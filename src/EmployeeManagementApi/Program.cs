using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.Middleware;
using EmployeeManagementApi.Models;
using EmployeeManagementApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ---------- Serilog ----------
// Logging estructurado a consola; Render/Docker capturan stdout automáticamente.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Render (y otros PaaS) inyectan el puerto vía la variable de entorno PORT.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(int.Parse(port)));

// Detrás del proxy de Render la IP real del cliente llega en X-Forwarded-For.
// Sin esto, el rate limiting agruparía a todos los clientes bajo la IP del proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear(); // la IP del proxy de Render no es fija
    options.KnownProxies.Clear();
});

// ---------- Servicios ----------
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()
                );

            var response = new ErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Uno o más campos no son válidos.",
                ValidationErrors = errors,
                TraceId = context.HttpContext.TraceIdentifier
            };

            return new BadRequestObjectResult(response);
        };
    });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTION")
    ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'DefaultConnection'.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IDemoService, DemoService>();

// ---------- Demo ----------
// El admin inicial y el superusuario siempre quedan protegidos, además de Demo:ProtectedUsers.
var initialAdminUsername = builder.Configuration["InitialAdmin:Username"] ?? "admin";
var superAdminUsername = builder.Configuration["SuperAdmin:Username"];
builder.Services.Configure<DemoSettings>(builder.Configuration.GetSection("Demo"));
builder.Services.PostConfigure<DemoSettings>(options =>
{
    options.ProtectedUsers = string.Join(',', options.ProtectedUsers, initialAdminUsername, superAdminUsername);
    options.ResetUsers = string.Join(',', initialAdminUsername, superAdminUsername);
});

// ---------- JWT ----------
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("No se configuró la sección 'Jwt'.");

if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) || jwtSettings.SecretKey.Length < 32)
    throw new InvalidOperationException("Jwt:SecretKey debe tener al menos 32 caracteres.");

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    // Un token firmado sigue siendo válido hasta que expira; aquí se rechaza si la cuenta
    // ya no existe, se desactivó o cambió de rol (por ejemplo, tras reiniciar la demo).
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var username = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleClaim = context.Principal?.FindFirstValue(ClaimTypes.Role);
            if (username is null || !Enum.TryParse<UserRole>(roleClaim, out var role))
            {
                context.Fail("Token sin usuario o rol válido.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var stillValid = await db.Users.AnyAsync(u => u.Username == username && u.IsActive && u.Role == role);
            if (!stillValid)
                context.Fail("La cuenta ya no está activa o cambió de rol.");
        }
    };
});

builder.Services.AddAuthorization();

// ---------- Rate limiting ----------
// Límite global razonable + límite estricto específico para /api/auth/login (fuerza bruta).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Reinicio de la demo: un solo contador para todos, es una operación pesada.
    options.AddPolicy("demo-reset", _ =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "demo-reset",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Sistema de Gestión de Empleados API",
        Version = "v1",
        Description = "API REST para la gestión de empleados y departamentos (CRUD, búsqueda, filtros, reportes, autenticación JWT)."
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    // Soporte de autenticación Bearer en la UI de Swagger (botón "Authorize").
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Ingresa el token JWT así: Bearer {tu token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        // En local se permite cualquier origen (Vite en localhost:5173, etc.).
        // En producción solo AllowedOrigins; si está vacío, ningún origen de navegador.
        if (builder.Environment.IsDevelopment())
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// ---------- Middleware pipeline ----------
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseSerilogRequestLogging(); // log estructurado de cada request (método, ruta, status, duración)
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Management API v1");
    options.RoutePrefix = string.Empty; // Swagger UI queda en la raíz "/"
});

app.UseCors("Default");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

// Aplica migraciones pendientes y asegura las cuentas de administración privadas.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (string.IsNullOrWhiteSpace(builder.Configuration["InitialAdmin:Password"]) && !db.Users.Any())
        Log.Warning("No se configuró InitialAdmin:Password; no se creó ningún usuario administrador. " +
                    "Configura esa variable y reinicia la app para poder autenticarte.");

    EnsureOwnerAdmin(db, "InitialAdmin");
    EnsureOwnerAdmin(db, "SuperAdmin");
}

app.Run();

// Crea la cuenta si no existe y sincroniza su contraseña con la configuración en cada arranque
// (para rotarla basta cambiar la variable de entorno y reiniciar).
void EnsureOwnerAdmin(AppDbContext db, string section)
{
    var username = builder.Configuration[$"{section}:Username"];
    var password = builder.Configuration[$"{section}:Password"];
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        return;

    var user = db.Users.FirstOrDefault(u => u.Username == username);
    if (user is null)
    {
        var configuredEmail = builder.Configuration[$"{section}:Email"];
        var email = string.IsNullOrWhiteSpace(configuredEmail) ? $"{username}@local" : configuredEmail.Trim().ToLower();

        // El correo es único: si otra cuenta ya lo usa, crear el admin rompería el arranque (error SQL 2601).
        var emailOwner = db.Users.Where(u => u.Email == email).Select(u => u.Username).FirstOrDefault();
        if (emailOwner is not null)
        {
            Log.Error("No se creó el admin '{Username}' ({Section}): el correo {Email} ya pertenece al usuario '{Owner}'. " +
                      "Usa otro {Section}:Email o pon {Section}:Username = '{Owner}'.",
                      username, section, email, emailOwner, section, section, emailOwner);
            return;
        }

        user = new User
        {
            Username = username,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        Log.Information("Usuario administrador '{Username}' creado.", username);
    }

    if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    user.Role = UserRole.Admin;
    user.IsActive = true;
    db.SaveChanges();
}

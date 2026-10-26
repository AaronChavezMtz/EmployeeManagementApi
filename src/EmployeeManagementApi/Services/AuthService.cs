using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EmployeeManagementApi.Services;

public class AuthService : IAuthService
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), workFactor: 12);

    private readonly AppDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, IOptions<JwtSettings> jwtOptions, ILogger<AuthService> logger)
    {
        _context = context;
        _jwtSettings = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive);

        // Mensaje genérico a propósito: no revela si el usuario existe o no (evita enumeración de cuentas).
        // Se verifica contra un hash ficticio si el usuario no existe, para que el tiempo de respuesta tampoco lo revele.
        var passwordOk = BCrypt.Net.BCrypt.Verify(dto.Password, user?.PasswordHash ?? DummyHash);
        if (user is null || !passwordOk)
        {
            _logger.LogWarning("Intento de login fallido para el usuario '{Username}'", dto.Username);
            throw new BusinessRuleException("Usuario o contraseña incorrectos");
        }

        _logger.LogInformation("Login exitoso: {Username}", user.Username);
        return GenerateToken(user);
    }

    public async Task<UserDto> RegisterAsync(RegisterUserDto dto)
    {
        var exists = await _context.Users.AnyAsync(u => u.Username == dto.Username || u.Email == dto.Email);
        if (exists)
            throw new BusinessRuleException("Ya existe un usuario con ese nombre de usuario o correo");

        var role = UserRoles.Parse(dto.Role);
        var employeeId = await UserLinkRules.ResolveEmployeeIdAsync(_context, role, dto.EmployeeId);

        var user = new User
        {
            Username = dto.Username.Trim(),
            Email = dto.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
            Role = role,
            EmployeeId = employeeId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuario creado: {Username} con rol {Role}", user.Username, user.Role);

        // No se devuelve un token del usuario nuevo: el admin podría usarlo para actuar en su nombre.
        await _context.Entry(user).Reference(u => u.Employee).LoadAsync();
        return UserDto.From(user);
    }

    private AuthResponseDto GenerateToken(User user)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
        var expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("userId", user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256)
        );

        return new AuthResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Username = user.Username,
            Role = user.Role.ToString(),
            ExpiresAtUtc = expires
        };
    }
}

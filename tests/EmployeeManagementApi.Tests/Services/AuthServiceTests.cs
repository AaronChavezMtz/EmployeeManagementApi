using EmployeeManagementApi.Common;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmployeeManagementApi.Tests.Services;

public class AuthServiceTests
{
    private static AuthService CreateService(out EmployeeManagementApi.Data.AppDbContext context)
    {
        context = TestDbContextFactory.Create();
        var jwtSettings = new JwtSettings
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SecretKey = "esta-es-una-llave-de-prueba-de-al-menos-32-caracteres",
            ExpiryMinutes = 30
        };
        return new AuthService(context, Options.Create(jwtSettings), NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_ConDatosValidos_GuardaContraseñaHasheadaYDevuelveToken()
    {
        var service = CreateService(out var context);

        var result = await service.RegisterAsync(new RegisterUserDto
        {
            Username = "carlos",
            Email = "carlos@test.com",
            Password = "ClaveSegura123!",
            Role = "Admin"
        });

        result.Token.Should().NotBeNullOrEmpty();
        result.Role.Should().Be("Admin");

        var userInDb = context.Users.First(u => u.Username == "carlos");
        userInDb.PasswordHash.Should().NotBe("ClaveSegura123!"); // nunca en texto plano
        BCrypt.Net.BCrypt.Verify("ClaveSegura123!", userInDb.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_ConUsuarioDuplicado_LanzaBusinessRuleException()
    {
        var service = CreateService(out _);
        var dto = new RegisterUserDto { Username = "ana", Email = "ana@test.com", Password = "Clave1234", Role = "Viewer" };
        await service.RegisterAsync(dto);

        var act = async () => await service.RegisterAsync(dto);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task LoginAsync_ConContraseñaIncorrecta_LanzaBusinessRuleException()
    {
        var service = CreateService(out _);
        await service.RegisterAsync(new RegisterUserDto
        {
            Username = "pedro", Email = "pedro@test.com", Password = "ClaveCorrecta1", Role = "Viewer"
        });

        var act = async () => await service.LoginAsync(new LoginDto { Username = "pedro", Password = "ClaveIncorrecta" });

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task LoginAsync_ConCredencialesCorrectas_DevuelveTokenValido()
    {
        var service = CreateService(out _);
        await service.RegisterAsync(new RegisterUserDto
        {
            Username = "sofia", Email = "sofia@test.com", Password = "MiClave123", Role = "Viewer"
        });

        var result = await service.LoginAsync(new LoginDto { Username = "sofia", Password = "MiClave123" });

        result.Token.Should().NotBeNullOrEmpty();
        result.Username.Should().Be("sofia");
        result.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }
}

using EmployeeManagementApi.Common;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using EmployeeManagementApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmployeeManagementApi.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public async Task UpdateAsync_ConUsuarioProtegido_LanzaBusinessRuleException()
    {
        var context = TestDbContextFactory.Create();
        var lector = new User { Username = "usuariolector", Email = "l@test.com", PasswordHash = "x", Role = UserRole.Viewer };
        context.Users.Add(lector);
        await context.SaveChangesAsync();
        var service = new UserService(context, Options.Create(new DemoSettings { ProtectedUsers = "admin, UsuarioLector" }));

        var act = async () => await service.UpdateAsync(lector.Id, new UpdateUserDto { Role = "Viewer", IsActive = false }, "otroadmin");

        await act.Should().ThrowAsync<BusinessRuleException>();
        context.Users.Single().IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("admin", true, true)]
    [InlineData("adminprueba", true, false)] // admin público de la demo: protegido, pero sin reinicio
    [InlineData("admin", false, false)]      // sin modo demo nadie reinicia
    public void CanReset_SoloCuentasDeReinicioConDemoHabilitada(string username, bool enabled, bool expected)
    {
        var demo = new DemoSettings { Enabled = enabled, ProtectedUsers = "adminprueba,admin", ResetUsers = "admin,dueño" };

        demo.CanReset(username).Should().Be(expected);
    }

    [Fact]
    public async Task UpdateAsync_ConUsuarioNoProtegido_ActualizaRolYEstado()
    {
        var context = TestDbContextFactory.Create();
        var user = new User { Username = "visitante", Email = "v@test.com", PasswordHash = "x", Role = UserRole.Admin };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new UserService(context, Options.Create(new DemoSettings { ProtectedUsers = "admin" }));

        var result = await service.UpdateAsync(user.Id, new UpdateUserDto { Role = "Viewer", IsActive = false }, "admin");

        result.Role.Should().Be("Viewer");
        result.IsActive.Should().BeFalse();
    }
}

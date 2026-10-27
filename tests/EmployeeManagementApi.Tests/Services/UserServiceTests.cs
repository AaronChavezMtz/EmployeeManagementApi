using EmployeeManagementApi.Data;
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
        var lector = new User { Username = "usuariolector", Email = "l@test.com", PasswordHash = "x", Role = UserRole.Employee };
        context.Users.Add(lector);
        await context.SaveChangesAsync();
        var service = new UserService(context, Options.Create(new DemoSettings { ProtectedUsers = "admin, UsuarioLector" }));

        var act = async () => await service.UpdateAsync(lector.Id, new UpdateUserDto { Role = "Employee", IsActive = false }, "otroadmin");

        await act.Should().ThrowAsync<BusinessRuleException>();
        context.Users.Single().IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("admin", true, true)]
    [InlineData("adminprueba", true, false)] // admin público de la demo: protegido, pero sin reinicio
    [InlineData("admin", false, false)]      // sin modo demo nadie reinicia
    public void CanReset_SoloCuentasDeReinicioConDemoHabilitada(string username, bool enabled, bool expected)
    {
        var demo = new DemoSettings { Enabled = enabled, ProtectedUsers = "adminprueba,admin", OwnerUsers = "admin,dueño" };

        demo.CanReset(username).Should().Be(expected);
    }

    private static Employee NewEmployee(string email) => new()
    {
        FirstName = "Ana", LastName = "Ruiz", Email = email, Position = "Dev", Salary = 1, DepartmentId = 1,
        HireDate = DateTime.UtcNow.AddYears(-2), BirthDate = DateTime.UtcNow.AddYears(-30)
    };

    [Fact]
    public async Task UpdateAsync_AEmployee_VinculaElEmpleado()
    {
        var context = TestDbContextFactory.Create();
        var employee = NewEmployee("ana@test.com");
        var user = new User { Username = "visitante", Email = "v@test.com", PasswordHash = "x", Role = UserRole.Admin };
        context.AddRange(employee, user);
        await context.SaveChangesAsync();
        var service = new UserService(context, Options.Create(new DemoSettings { ProtectedUsers = "admin" }));

        var result = await service.UpdateAsync(user.Id, new UpdateUserDto { Role = "Employee", IsActive = false, EmployeeId = employee.Id }, "admin");

        result.Role.Should().Be("Employee");
        result.IsActive.Should().BeFalse();
        result.EmployeeId.Should().Be(employee.Id);
        result.EmployeeName.Should().Be("Ana Ruiz");
    }

    [Fact]
    public async Task UpdateAsync_ConEmpleadoYaVinculadoAOtroUsuario_LanzaBusinessRuleException()
    {
        var context = TestDbContextFactory.Create();
        var employee = NewEmployee("ana@test.com");
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        context.Users.Add(new User { Username = "ana", Email = "a@test.com", PasswordHash = "x", Role = UserRole.Employee, EmployeeId = employee.Id });
        var otro = new User { Username = "otro", Email = "o@test.com", PasswordHash = "x", Role = UserRole.Admin };
        context.Users.Add(otro);
        await context.SaveChangesAsync();
        var service = new UserService(context, Options.Create(new DemoSettings()));

        var act = async () => await service.UpdateAsync(otro.Id, new UpdateUserDto { Role = "Employee", EmployeeId = employee.Id }, "admin");

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task GetLinkedEmployeeIdAsync_SinVinculo_LanzaBusinessRuleException()
    {
        var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Username = "suelto", Email = "s@test.com", PasswordHash = "x", Role = UserRole.Employee });
        await context.SaveChangesAsync();
        var service = new UserService(context, Options.Create(new DemoSettings()));

        var act = async () => await service.GetLinkedEmployeeIdAsync("suelto");

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UpdateAsync_DuenoPuedeEditarUnaCuentaProtegida()
    {
        var context = TestDbContextFactory.Create();
        var publico = new User { Username = "adminprueba", Email = "p@test.com", PasswordHash = "x", Role = UserRole.Admin };
        context.Users.Add(publico);
        await context.SaveChangesAsync();
        var demo = new DemoSettings { ProtectedUsers = "adminprueba,admin", OwnerUsers = "admin" };
        var service = new UserService(context, Options.Create(demo));

        var result = await service.UpdateAsync(publico.Id, new UpdateUserDto { Role = "Admin", IsActive = false }, "admin");

        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_MarcaCanEditSegunQuienConsulta()
    {
        var context = TestDbContextFactory.Create();
        context.Users.AddRange(
            new User { Username = "admin", Email = "a@test.com", PasswordHash = "x", Role = UserRole.Admin },
            new User { Username = "adminprueba", Email = "p@test.com", PasswordHash = "x", Role = UserRole.Admin },
            new User { Username = "visitante", Email = "v@test.com", PasswordHash = "x", Role = UserRole.Admin });
        await context.SaveChangesAsync();
        var service = new UserService(context, Options.Create(new DemoSettings { ProtectedUsers = "adminprueba,admin", OwnerUsers = "admin" }));

        var vistaDueno = (await service.GetAllAsync("admin")).ToDictionary(u => u.Username, u => u.CanEdit);
        var vistaPublica = (await service.GetAllAsync("adminprueba")).ToDictionary(u => u.Username, u => u.CanEdit);

        vistaDueno.Should().Equal(new Dictionary<string, bool> { ["admin"] = false, ["adminprueba"] = true, ["visitante"] = true });
        vistaPublica.Should().Equal(new Dictionary<string, bool> { ["admin"] = false, ["adminprueba"] = false, ["visitante"] = true });
    }

    private static (UserService Service, AppDbContext Context) WithUser(string username, string password, bool mustChange = true)
    {
        var context = TestDbContextFactory.Create();
        context.Users.Add(new User
        {
            Username = username, Email = $"{username}@test.com", Role = UserRole.Employee,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4), MustChangePassword = mustChange
        });
        context.SaveChanges();
        return (new UserService(context, Options.Create(new DemoSettings { ProtectedUsers = "usuariolector" })), context);
    }

    [Fact]
    public async Task ChangePasswordAsync_ConContrasenaActualCorrecta_CambiaYQuitaLaObligacion()
    {
        var (service, context) = WithUser("ana", "Temporal123");

        await service.ChangePasswordAsync("ana", new ChangePasswordDto { CurrentPassword = "Temporal123", NewPassword = "MiClaveNueva1" });

        var user = context.Users.Single();
        BCrypt.Net.BCrypt.Verify("MiClaveNueva1", user.PasswordHash).Should().BeTrue();
        user.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePasswordAsync_ConContrasenaActualIncorrecta_LanzaBusinessRuleException()
    {
        var (service, _) = WithUser("ana", "Temporal123");

        var act = async () => await service.ChangePasswordAsync("ana", new ChangePasswordDto { CurrentPassword = "otra", NewPassword = "MiClaveNueva1" });

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task ChangePasswordAsync_EnCuentaProtegida_LanzaBusinessRuleException()
    {
        var (service, _) = WithUser("usuariolector", "lector_123", mustChange: false);

        var act = async () => await service.ChangePasswordAsync("usuariolector", new ChangePasswordDto { CurrentPassword = "lector_123", NewPassword = "Secuestrada1" });

        await act.Should().ThrowAsync<BusinessRuleException>();
    }
}

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
        var demo = new DemoSettings { Enabled = enabled, ProtectedUsers = "adminprueba,admin", ResetUsers = "admin,dueño" };

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
}

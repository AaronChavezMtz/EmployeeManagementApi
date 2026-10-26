using EmployeeManagementApi.Common;
using EmployeeManagementApi.Models;
using EmployeeManagementApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmployeeManagementApi.Tests.Services;

public class DemoServiceTests
{
    [Fact]
    public async Task ResetAsync_BorraUsuariosNoProtegidosYResiembraDatos()
    {
        var context = TestDbContextFactory.Create();
        context.Users.AddRange(
            new User { Username = "admin", Email = "a@test.com", PasswordHash = "x", Role = UserRole.Admin },
            new User { Username = "usuariolector", Email = "l@test.com", PasswordHash = "x", Role = UserRole.Employee },
            new User { Username = "vandalo", Email = "v@test.com", PasswordHash = "x", Role = UserRole.Admin });
        context.Departments.Add(new Department { Name = "Departamento basura" });
        await context.SaveChangesAsync();
        var service = new DemoService(context, Options.Create(new DemoSettings { ProtectedUsers = "admin,usuariolector" }),
            NullLogger<DemoService>.Instance);

        await service.ResetAsync();

        context.Users.Select(u => u.Username).Should().BeEquivalentTo(new[] { "admin", "usuariolector" });
        context.Departments.Should().HaveCount(4).And.NotContain(d => d.Name == "Departamento basura");
        context.Employees.Should().HaveCount(12);
        context.EmployeeHistories.Should().HaveCount(14); // 12 altas + 2 bajas
        var lector = context.Users.Single(u => u.Username == "usuariolector");
        lector.EmployeeId.Should().NotBeNull(); // el portal de la demo siempre tiene una ficha
        context.Employees.Single(e => e.Id == lector.EmployeeId).IsActive.Should().BeTrue();
    }
}

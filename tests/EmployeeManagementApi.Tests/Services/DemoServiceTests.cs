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
            new User { Username = "vandalo", Email = "v@test.com", PasswordHash = "x", Role = UserRole.Admin });
        context.Departments.Add(new Department { Name = "Departamento basura" });
        await context.SaveChangesAsync();
        var service = new DemoService(context, Options.Create(new DemoSettings { ProtectedUsers = "admin" }),
            NullLogger<DemoService>.Instance);

        await service.ResetAsync();

        context.Users.Select(u => u.Username).Should().BeEquivalentTo(new[] { "admin" });
        context.Departments.Should().HaveCount(4).And.NotContain(d => d.Name == "Departamento basura");
        context.Employees.Should().HaveCount(12);
        context.EmployeeHistories.Should().HaveCount(12);
    }
}

using System.Text.Json;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EmployeeManagementApi.Tests.Data;

public class UtcTimestampTests
{
    [Fact]
    public async Task LosInstantesSeLeenComoUtcYLasFechasDeCalendarioNo()
    {
        // Dos contextos sobre la misma base: el segundo lee "desde disco", como en producción.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var write = new AppDbContext(options))
        {
            write.Database.EnsureCreated();
            var employee = new Employee
            {
                FirstName = "Ana", LastName = "Ruiz", Email = "ana@test.com", Position = "Dev", Salary = 1, DepartmentId = 1,
                HireDate = new DateTime(2025, 3, 1), BirthDate = new DateTime(1990, 6, 15)
            };
            write.Employees.Add(employee);
            await write.SaveChangesAsync();
            write.EmployeeHistories.Add(new EmployeeHistory
            {
                EmployeeId = employee.Id, ChangeType = HistoryChangeType.Updated, ChangedBy = "admin",
                ChangedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 10, 27, 3, 7, 0), DateTimeKind.Unspecified)
            });
            await write.SaveChangesAsync();
        }

        await using var read = new AppDbContext(options);
        var history = await read.EmployeeHistories.AsNoTracking().SingleAsync();
        var hired = await read.Employees.AsNoTracking().SingleAsync();

        history.ChangedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        JsonSerializer.Serialize(history.ChangedAtUtc).Should().Be("\"2026-10-27T03:07:00Z\""); // el navegador lo convierte a hora local
        hired.HireDate.Kind.Should().NotBe(DateTimeKind.Utc); // un día de calendario no se desplaza
    }
}

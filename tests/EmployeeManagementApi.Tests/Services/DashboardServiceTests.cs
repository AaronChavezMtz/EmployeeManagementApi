using EmployeeManagementApi.Models;
using EmployeeManagementApi.Services;
using FluentAssertions;
using Xunit;

namespace EmployeeManagementApi.Tests.Services;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetKpisAsync_CalculaRotacionDeLosUltimos12MesesSobrePlantillaPromedio()
    {
        var context = TestDbContextFactory.Create();
        var today = DateTime.UtcNow.Date;
        Employee Emp(string email, int hiredMonthsAgo, int? terminatedMonthsAgo = null) => new()
        {
            FirstName = "A", LastName = "B", Email = email, Position = "P", Salary = 1, DepartmentId = 1,
            BirthDate = today.AddYears(-30), HireDate = today.AddMonths(-hiredMonthsAgo),
            IsActive = terminatedMonthsAgo is null,
            TerminationDate = terminatedMonthsAgo is { } t ? today.AddMonths(-t) : null,
            TerminationReason = terminatedMonthsAgo is null ? null : TerminationReason.Resignation
        };
        context.Employees.AddRange(
            Emp("a@t.com", 24), Emp("b@t.com", 24), Emp("c@t.com", 24),
            Emp("d@t.com", 24, terminatedMonthsAgo: 3),   // baja dentro de la ventana
            Emp("e@t.com", 36, terminatedMonthsAgo: 20)); // baja antigua: no cuenta
        await context.SaveChangesAsync();

        var kpis = await new DashboardService(context).GetKpisAsync();

        // Plantilla al inicio de la ventana = 4, hoy = 3 → promedio 3.5; 1 baja → 28.6 %
        kpis.TerminationsLast12Months.Should().Be(1);
        kpis.TurnoverRatePercent.Should().Be(28.6);
        kpis.ActiveHeadcount.Should().Be(3);
        kpis.TerminationsByReason.Should().ContainSingle(r => r.Reason == "Resignation" && r.Count == 1);
        kpis.HiringTrend.Should().HaveCount(12);
    }
}

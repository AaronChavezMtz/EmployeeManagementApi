using System.Globalization;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;
    private static readonly CultureInfo SpanishCulture = new("es-MX");

    public DashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardKpisDto> GetKpisAsync()
    {
        // La plantilla de una pyme cabe en memoria; así los cálculos por fecha quedan legibles.
        var employees = await _context.Employees.AsNoTracking()
            .Select(e => new { e.HireDate, e.TerminationDate, e.TerminationReason, e.IsActive })
            .ToListAsync();

        var today = DateTime.UtcNow.Date;
        var startOfWindow = new DateTime(today.Year, today.Month, 1).AddMonths(-11);

        // Plantilla en una fecha: contratados hasta ese día y sin baja (o con baja posterior).
        int HeadcountAt(DateTime date) =>
            employees.Count(e => e.HireDate.Date <= date && (e.TerminationDate is null || e.TerminationDate.Value.Date > date));

        var terminations = employees
            .Where(e => e.TerminationDate is { } d && d.Date >= startOfWindow && d.Date <= today)
            .ToList();

        var averageHeadcount = (HeadcountAt(startOfWindow) + HeadcountAt(today)) / 2.0;
        var turnoverRate = averageHeadcount == 0 ? 0 : Math.Round(terminations.Count / averageHeadcount * 100, 1);

        var active = employees.Where(e => e.IsActive).ToList();
        var averageTenure = active.Count == 0 ? 0 : Math.Round(active.Average(e => (today - e.HireDate.Date).TotalDays) / 365.25, 1);

        // Se completan los 12 meses aunque algunos no tengan movimientos, para que la gráfica no tenga huecos.
        var trend = new List<MonthlyHiresDto>();
        for (var i = 0; i < 12; i++)
        {
            var month = startOfWindow.AddMonths(i);
            trend.Add(new MonthlyHiresDto
            {
                Month = month.ToString("yyyy-MM"),
                MonthLabel = month.ToString("MMM yyyy", SpanishCulture),
                Hires = employees.Count(e => e.HireDate.Year == month.Year && e.HireDate.Month == month.Month),
                Terminations = terminations.Count(e => e.TerminationDate!.Value.Year == month.Year && e.TerminationDate.Value.Month == month.Month)
            });
        }

        return new DashboardKpisDto
        {
            TotalHeadcount = employees.Count,
            ActiveHeadcount = active.Count,
            TurnoverRatePercent = turnoverRate,
            TerminationsLast12Months = terminations.Count,
            AverageTenureYears = averageTenure,
            TerminationsByReason = terminations
                .GroupBy(e => e.TerminationReason?.ToString() ?? "Other")
                .Select(g => new TerminationsByReasonDto { Reason = g.Key, Count = g.Count() })
                .OrderByDescending(r => r.Count)
                .ToList(),
            HiringTrend = trend
        };
    }
}

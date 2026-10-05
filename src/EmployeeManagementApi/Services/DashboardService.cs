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
        var total = await _context.Employees.CountAsync();
        var active = await _context.Employees.CountAsync(e => e.IsActive);
        var inactive = total - active;

        var turnoverRate = total == 0 ? 0 : Math.Round((double)inactive / total * 100, 1);

        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-11);
        var startOfWindow = new DateTime(twelveMonthsAgo.Year, twelveMonthsAgo.Month, 1);

        var hiresRaw = await _context.Employees
            .Where(e => e.HireDate >= startOfWindow)
            .GroupBy(e => new { e.HireDate.Year, e.HireDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        // Se completan los 12 meses aunque algunos no tengan contrataciones, para que la gráfica no tenga huecos.
        var trend = new List<MonthlyHiresDto>();
        for (var i = 0; i < 12; i++)
        {
            var month = startOfWindow.AddMonths(i);
            var match = hiresRaw.FirstOrDefault(h => h.Year == month.Year && h.Month == month.Month);
            trend.Add(new MonthlyHiresDto
            {
                Month = month.ToString("yyyy-MM"),
                MonthLabel = month.ToString("MMM yyyy", SpanishCulture),
                Hires = match?.Count ?? 0
            });
        }

        return new DashboardKpisDto
        {
            TotalHeadcount = total,
            ActiveHeadcount = active,
            TurnoverRatePercent = turnoverRate,
            HiringTrend = trend
        };
    }
}

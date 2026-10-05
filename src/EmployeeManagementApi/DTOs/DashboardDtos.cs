namespace EmployeeManagementApi.DTOs;

public class MonthlyHiresDto
{
    public string Month { get; set; } = string.Empty; // "2026-01"
    public string MonthLabel { get; set; } = string.Empty; // "Ene 2026"
    public int Hires { get; set; }
}

public class DashboardKpisDto
{
    public int TotalHeadcount { get; set; }
    public int ActiveHeadcount { get; set; }
    public double TurnoverRatePercent { get; set; } // % de empleados dados de baja sobre el total histórico
    public List<MonthlyHiresDto> HiringTrend { get; set; } = new();
}

namespace EmployeeManagementApi.DTOs;

public class MonthlyHiresDto
{
    public string Month { get; set; } = string.Empty; // "2026-01"
    public string MonthLabel { get; set; } = string.Empty; // "Ene 2026"
    public int Hires { get; set; }
    public int Terminations { get; set; }
}

public class TerminationsByReasonDto
{
    public string Reason { get; set; } = string.Empty; // "Resignation", "Dismissal", ...
    public int Count { get; set; }
}

public class DashboardKpisDto
{
    public int TotalHeadcount { get; set; }
    public int ActiveHeadcount { get; set; }

    // Rotación de los últimos 12 meses: bajas del periodo / plantilla promedio del periodo.
    public double TurnoverRatePercent { get; set; }
    public int TerminationsLast12Months { get; set; }
    public double AverageTenureYears { get; set; } // antigüedad promedio de los activos
    public List<TerminationsByReasonDto> TerminationsByReason { get; set; } = new();
    public List<MonthlyHiresDto> HiringTrend { get; set; } = new();
}

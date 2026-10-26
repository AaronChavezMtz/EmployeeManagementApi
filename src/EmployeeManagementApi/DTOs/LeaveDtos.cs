using System.ComponentModel.DataAnnotations;
using EmployeeManagementApi.Models;

namespace EmployeeManagementApi.DTOs;

public class LeaveRequestDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public LeaveType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Days { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateLeaveRequestDto
{
    [Required(ErrorMessage = "El tipo de ausencia es obligatorio")]
    public LeaveType? Type { get; set; }

    [Required(ErrorMessage = "La fecha de inicio es obligatoria")]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "La fecha de fin es obligatoria")]
    public DateTime EndDate { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }
}

public class ReviewLeaveRequestDto
{
    [StringLength(300)]
    public string? Comment { get; set; } // obligatorio al rechazar
}

public class VacationBalanceDto
{
    public int YearsOfService { get; set; }
    public DateTime PeriodStart { get; set; } // último aniversario
    public DateTime PeriodEnd { get; set; }   // próximo aniversario
    public int EntitledDays { get; set; }     // días que da la LFT para este periodo
    public int ApprovedDays { get; set; }
    public int PendingDays { get; set; }
    public int AvailableDays { get; set; }    // derecho - aprobados - pendientes
}

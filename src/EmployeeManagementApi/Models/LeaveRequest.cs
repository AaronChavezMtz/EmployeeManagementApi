namespace EmployeeManagementApi.Models;

public enum LeaveType
{
    Vacation,  // descuenta del saldo de vacaciones
    Sick,      // incapacidad
    Personal   // permiso personal
}

public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

// Solicitud de ausencia de un empleado; RH la aprueba o la rechaza.
public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public LeaveType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Days { get; set; } // días hábiles (lunes a viernes)
    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

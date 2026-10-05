namespace EmployeeManagementApi.Models;

public enum HistoryChangeType
{
    Created,
    Updated,
    DepartmentChanged,
    SalaryChanged,
    Activated,
    Deactivated
}

// Bitácora de cambios relevantes de un empleado (quién, qué, cuándo).
public class EmployeeHistory
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public HistoryChangeType ChangeType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedBy { get; set; } = string.Empty; // username de quien hizo el cambio
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}

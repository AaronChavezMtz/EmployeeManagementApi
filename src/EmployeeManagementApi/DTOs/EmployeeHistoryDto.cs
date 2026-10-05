using EmployeeManagementApi.Models;

namespace EmployeeManagementApi.DTOs;

public class EmployeeHistoryDto
{
    public int Id { get; set; }
    public HistoryChangeType ChangeType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}

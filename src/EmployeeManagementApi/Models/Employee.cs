namespace EmployeeManagementApi.Models;

public enum TerminationReason
{
    Resignation,  // renuncia voluntaria
    Dismissal,    // despido
    ContractEnd,  // término de contrato
    Other
}

public class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Position { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime BirthDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Se llenan al dar de baja y se limpian al recontratar.
    public DateTime? TerminationDate { get; set; }
    public TerminationReason? TerminationReason { get; set; }

    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
}

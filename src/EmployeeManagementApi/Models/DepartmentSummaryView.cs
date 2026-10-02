namespace EmployeeManagementApi.Models;

// Entidad sin clave (keyless) mapeada a la vista SQL vw_DepartmentSummary.
public class DepartmentSummaryView
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }
    public decimal AverageSalary { get; set; }
    public decimal TotalPayroll { get; set; }
}

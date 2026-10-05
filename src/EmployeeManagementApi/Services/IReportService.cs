namespace EmployeeManagementApi.Services;

public interface IReportService
{
    Task<byte[]> ExportEmployeesToExcelAsync(EmployeeManagementApi.DTOs.EmployeeQueryParameters query);
    Task<byte[]> ExportDepartmentSummaryToExcelAsync();
    Task<byte[]> ExportDepartmentSummaryToPdfAsync();
}

using EmployeeManagementApi.DTOs;

namespace EmployeeManagementApi.Services;

public interface IDepartmentService
{
    Task<IEnumerable<DepartmentDto>> GetAllAsync();
    Task<DepartmentDto> GetByIdAsync(int id);
    Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto);
    Task<DepartmentDto> UpdateAsync(int id, UpdateDepartmentDto dto);
    Task DeleteAsync(int id);

    // Reporte basado en la vista SQL vw_DepartmentSummary
    Task<IEnumerable<DepartmentSummaryDto>> GetSummaryAsync();
}

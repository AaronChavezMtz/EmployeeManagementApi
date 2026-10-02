using EmployeeManagementApi.Common;
using EmployeeManagementApi.DTOs;

namespace EmployeeManagementApi.Services;

public interface IEmployeeService
{
    Task<PagedResultDto<EmployeeDto>> GetAllAsync(EmployeeQueryParameters query);
    Task<EmployeeDto> GetByIdAsync(int id);
    Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto);
    Task<EmployeeDto> UpdateAsync(int id, UpdateEmployeeDto dto);
    Task DeleteAsync(int id);

    // Búsqueda avanzada delegada al procedimiento almacenado sp_SearchEmployees
    Task<IEnumerable<EmployeeDto>> SearchWithStoredProcedureAsync(string? searchTerm, int? departmentId, bool? isActive);
}

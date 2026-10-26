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

    // Bitácora de cambios de un empleado específico
    Task<IEnumerable<EmployeeHistoryDto>> GetHistoryAsync(int employeeId);
}

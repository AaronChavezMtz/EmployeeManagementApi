using EmployeeManagementApi.Common;
using EmployeeManagementApi.DTOs;

namespace EmployeeManagementApi.Services;

public interface IEmployeeService
{
    Task<PagedResultDto<EmployeeDto>> GetAllAsync(EmployeeQueryParameters query);
    Task<EmployeeDto> GetByIdAsync(int id);
    Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto);
    Task<EmployeeDto> UpdateAsync(int id, UpdateEmployeeDto dto);
    Task<EmployeeDto> TerminateAsync(int id, TerminateEmployeeDto dto);
    Task<EmployeeDto> RehireAsync(int id, RehireEmployeeDto dto);

    // Único dato que el propio empleado puede cambiar desde el portal.
    Task<EmployeeDto> UpdatePhoneAsync(int id, string? phone);

    // Bitácora de cambios de un empleado específico
    Task<IEnumerable<EmployeeHistoryDto>> GetHistoryAsync(int employeeId);
}

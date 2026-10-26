using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;

namespace EmployeeManagementApi.Services;

public interface ILeaveService
{
    // Portal del empleado
    Task<IEnumerable<LeaveRequestDto>> GetForEmployeeAsync(int employeeId);
    Task<VacationBalanceDto> GetBalanceAsync(int employeeId);
    Task<LeaveRequestDto> CreateAsync(int employeeId, CreateLeaveRequestDto dto);
    Task<LeaveRequestDto> CancelAsync(int employeeId, int requestId);

    // RH
    Task<IEnumerable<LeaveRequestDto>> GetAllAsync(LeaveStatus? status);
    Task<LeaveRequestDto> ApproveAsync(int requestId, string reviewer, string? comment);
    Task<LeaveRequestDto> RejectAsync(int requestId, string reviewer, string? comment);
}

using EmployeeManagementApi.DTOs;

namespace EmployeeManagementApi.Services;

public interface IDashboardService
{
    Task<DashboardKpisDto> GetKpisAsync();
}

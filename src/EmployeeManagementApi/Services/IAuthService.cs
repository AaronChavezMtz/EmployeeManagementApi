using EmployeeManagementApi.DTOs;

namespace EmployeeManagementApi.Services;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RegisterAsync(RegisterUserDto dto);
}

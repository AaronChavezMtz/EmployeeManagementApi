using EmployeeManagementApi.DTOs;

namespace EmployeeManagementApi.Services;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync(string currentUsername);
    Task<UserDto> UpdateAsync(int id, UpdateUserDto dto, string currentUsername);

    // Empleado vinculado al usuario (portal del empleado).
    Task<int> GetLinkedEmployeeIdAsync(string username);
}

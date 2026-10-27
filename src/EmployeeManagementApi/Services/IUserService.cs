using EmployeeManagementApi.DTOs;

namespace EmployeeManagementApi.Services;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync(string currentUsername);
    Task<UserDto> UpdateAsync(int id, UpdateUserDto dto, string currentUsername);

    // Empleado vinculado al usuario (portal del empleado).
    Task<int> GetLinkedEmployeeIdAsync(string username);

    // Cambio de contraseña del propio usuario (no aplica a cuentas protegidas).
    Task ChangePasswordAsync(string username, ChangePasswordDto dto);

    // Acceso al portal: usuario Employee vinculado, con contraseña temporal.
    Task EnsurePortalUsernameAvailableAsync(string employeeEmail);
    Task<PortalAccessDto> CreatePortalAccessAsync(int employeeId);
}

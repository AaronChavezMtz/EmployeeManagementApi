using EmployeeManagementApi.Common;

namespace EmployeeManagementApi.Models;

public enum UserRole
{
    Employee = 0, // portal del empleado: solo su propia información
    Admin = 1     // RH: acceso completo
}

public static class UserRoles
{
    // Enum.TryParse acepta cualquier número ("7"); IsDefined lo descarta.
    public static UserRole Parse(string? value) =>
        Enum.TryParse<UserRole>(value, ignoreCase: true, out var role) && Enum.IsDefined(role)
            ? role
            : throw new BusinessRuleException("El rol debe ser 'Admin' o 'Employee'");
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Ficha de empleado a la que accede este usuario (obligatoria para el rol Employee).
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
}

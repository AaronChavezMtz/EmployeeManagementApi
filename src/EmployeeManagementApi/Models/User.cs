namespace EmployeeManagementApi.Models;

public enum UserRole
{
    Viewer = 0,  // solo lectura (GET)
    Admin = 1    // acceso completo (CRUD)
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

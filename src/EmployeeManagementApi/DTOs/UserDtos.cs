using System.ComponentModel.DataAnnotations;

namespace EmployeeManagementApi.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateUserDto
{
    [Required]
    public string Role { get; set; } = string.Empty; // "Admin" o "Viewer"
    public bool IsActive { get; set; } = true;
}

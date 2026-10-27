using System.ComponentModel.DataAnnotations;

namespace EmployeeManagementApi.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "El usuario es obligatorio")]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterUserDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Employee"; // "Admin" o "Employee"

    public int? EmployeeId { get; set; } // obligatorio si Role = "Employee"
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool MustChangePassword { get; set; }
}

public class ChangePasswordDto
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria")]
    [StringLength(100)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres")]
    public string NewPassword { get; set; } = string.Empty;
}

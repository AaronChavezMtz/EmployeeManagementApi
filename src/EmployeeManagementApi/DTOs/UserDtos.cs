using System.ComponentModel.DataAnnotations;
using EmployeeManagementApi.Models;

namespace EmployeeManagementApi.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsProtected { get; set; } // cuenta de demo o del dueño
    public bool CanEdit { get; set; }     // si el usuario que consulta puede modificarla
    public int? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime CreatedAt { get; set; }

    // Requiere haber cargado user.Employee para mostrar el nombre.
    public static UserDto From(User u, bool isProtected = false, bool canEdit = true) => new()
    {
        Id = u.Id, Username = u.Username, Email = u.Email,
        Role = u.Role.ToString(), IsActive = u.IsActive, IsProtected = isProtected, CanEdit = canEdit,
        EmployeeId = u.EmployeeId,
        EmployeeName = u.Employee is null ? null : $"{u.Employee.FirstName} {u.Employee.LastName}",
        CreatedAt = u.CreatedAt
    };
}

public class UpdateUserDto
{
    [Required]
    public string Role { get; set; } = string.Empty; // "Admin" o "Employee"
    public bool IsActive { get; set; } = true;
    public int? EmployeeId { get; set; } // obligatorio si Role = "Employee"
}

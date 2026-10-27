using System.ComponentModel.DataAnnotations;
using EmployeeManagementApi.Models;
using EmployeeManagementApi.Common;

namespace EmployeeManagementApi.DTOs;

public class EmployeeDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Position { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime BirthDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public int DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    public string? PortalUsername { get; set; }     // solo en el detalle: usuario vinculado, si lo hay
    public PortalAccessDto? PortalAccess { get; set; } // solo al crear el acceso: credenciales temporales
}

// Credenciales que se muestran una única vez al admin para que se las entregue al empleado.
public class PortalAccessDto
{
    public string Username { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
}

public class CreateEmployeeDto
{
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 80 caracteres")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 80 caracteres")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del correo no es válido")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "El formato del teléfono no es válido")]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "El puesto es obligatorio")]
    [StringLength(100)]
    public string Position { get; set; } = string.Empty;

    [Range(0.01, 10_000_000, ErrorMessage = "El salario debe ser mayor a 0")]
    public decimal Salary { get; set; }

    [Required(ErrorMessage = "La fecha de contratación es obligatoria")]
    public DateTime HireDate { get; set; }

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria")]
    [MinimumAge(18)]
    public DateTime BirthDate { get; set; }

    [Required(ErrorMessage = "El departamento es obligatorio")]
    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un departamento válido")]
    public int DepartmentId { get; set; }

    public bool GrantPortalAccess { get; set; } // crea también su usuario del portal
}

public class UpdateEmployeeDto
{
    [Required]
    [StringLength(80, MinimumLength = 2)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(80, MinimumLength = 2)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Required]
    [StringLength(100)]
    public string Position { get; set; } = string.Empty;

    [Range(0.01, 10_000_000)]
    public decimal Salary { get; set; }

    [Required]
    [MinimumAge(18)]
    public DateTime BirthDate { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int DepartmentId { get; set; }
}

// POST /api/employees/{id}/terminate
public class TerminateEmployeeDto
{
    [Required(ErrorMessage = "La fecha de baja es obligatoria")]
    public DateTime TerminationDate { get; set; }

    [Required(ErrorMessage = "El motivo de baja es obligatorio")]
    public TerminationReason? Reason { get; set; }
}

// POST /api/employees/{id}/rehire
public class RehireEmployeeDto
{
    [Required(ErrorMessage = "La fecha de recontratación es obligatoria")]
    public DateTime HireDate { get; set; }
}

// Parámetros de búsqueda y filtrado para GET /api/employees
public class EmployeeQueryParameters
{
    public string? Search { get; set; }              // busca en nombre, apellido, correo, puesto
    public int? DepartmentId { get; set; }
    public bool? IsActive { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public DateTime? HiredAfter { get; set; }
    public DateTime? HiredBefore { get; set; }
    public string? SortBy { get; set; } = "lastname"; // lastname | salary | hiredate | department
    public bool Descending { get; set; } = false;

    private const int MaxPageSize = 100;
    private const int MaxPageNumber = 10_000; // evita desbordar (PageNumber - 1) * PageSize
    private int _pageSize = 10;
    private int _pageNumber = 1;
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = Math.Clamp(value, 1, MaxPageNumber);
    }
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    // Solo para reportes internos: salta el tope de MaxPageSize (no lo enlaza el model binding).
    internal void SetPageSizeUnchecked(int size) => _pageSize = size;
}

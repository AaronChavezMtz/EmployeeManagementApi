using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagementApi.Controllers;

// Portal del empleado: todo se resuelve a partir del usuario del token, nunca de un id en la URL,
// así un empleado no puede consultar la ficha de otro.
[ApiController]
[Route("api/me")]
[Produces("application/json")]
[Authorize(Roles = "Employee")]
public class MeController : ControllerBase
{
    private readonly IUserService _users;
    private readonly IEmployeeService _employees;

    public MeController(IUserService users, IEmployeeService employees)
    {
        _users = users;
        _employees = employees;
    }

    private Task<int> MyEmployeeIdAsync() =>
        _users.GetLinkedEmployeeIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

    /// <summary>Ficha del empleado vinculado al usuario actual.</summary>
    [HttpGet]
    public async Task<ActionResult<EmployeeDto>> GetProfile()
        => Ok(await _employees.GetByIdAsync(await MyEmployeeIdAsync()));

    /// <summary>Historial de cambios del empleado actual.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<EmployeeHistoryDto>>> GetHistory()
        => Ok(await _employees.GetHistoryAsync(await MyEmployeeIdAsync()));

    /// <summary>Actualiza el teléfono de contacto (único dato editable por el empleado).</summary>
    [HttpPut("phone")]
    public async Task<ActionResult<EmployeeDto>> UpdatePhone([FromBody] UpdatePhoneDto dto)
        => Ok(await _employees.UpdatePhoneAsync(await MyEmployeeIdAsync(), dto.Phone));
}

public class UpdatePhoneDto
{
    [Phone(ErrorMessage = "El formato del teléfono no es válido")]
    [StringLength(20)]
    public string? Phone { get; set; }
}

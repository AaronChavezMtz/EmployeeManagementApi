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
    private readonly ILeaveService _leave;

    public MeController(IUserService users, IEmployeeService employees, ILeaveService leave)
    {
        _users = users;
        _employees = employees;
        _leave = leave;
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

    /// <summary>Saldo de vacaciones del periodo de aniversario actual (LFT).</summary>
    [HttpGet("vacation-balance")]
    public async Task<ActionResult<VacationBalanceDto>> GetVacationBalance()
        => Ok(await _leave.GetBalanceAsync(await MyEmployeeIdAsync()));

    /// <summary>Solicitudes de ausencia del empleado actual.</summary>
    [HttpGet("leave-requests")]
    public async Task<ActionResult<IEnumerable<LeaveRequestDto>>> GetLeaveRequests()
        => Ok(await _leave.GetForEmployeeAsync(await MyEmployeeIdAsync()));

    /// <summary>Crea una solicitud de vacaciones, incapacidad o permiso.</summary>
    [HttpPost("leave-requests")]
    public async Task<ActionResult<LeaveRequestDto>> CreateLeaveRequest([FromBody] CreateLeaveRequestDto dto)
        => Ok(await _leave.CreateAsync(await MyEmployeeIdAsync(), dto));

    /// <summary>Cancela una solicitud propia pendiente, o aprobada que aún no empieza.</summary>
    [HttpPost("leave-requests/{id:int}/cancel")]
    public async Task<ActionResult<LeaveRequestDto>> CancelLeaveRequest(int id)
        => Ok(await _leave.CancelAsync(await MyEmployeeIdAsync(), id));
}

public class UpdatePhoneDto
{
    [Phone(ErrorMessage = "El formato del teléfono no es válido")]
    [StringLength(20)]
    public string? Phone { get; set; }
}

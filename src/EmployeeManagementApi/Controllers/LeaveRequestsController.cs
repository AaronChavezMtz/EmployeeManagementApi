using System.Security.Claims;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using EmployeeManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagementApi.Controllers;

// Bandeja de RH: revisar las solicitudes de ausencia de todos los empleados.
[ApiController]
[Route("api/leave-requests")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class LeaveRequestsController : ControllerBase
{
    private readonly ILeaveService _service;

    public LeaveRequestsController(ILeaveService service)
    {
        _service = service;
    }

    private string Reviewer => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "sistema";

    /// <summary>Lista solicitudes (pendientes primero), opcionalmente filtradas por estado.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaveRequestDto>>> GetAll([FromQuery] LeaveStatus? status)
        => Ok(await _service.GetAllAsync(status));

    /// <summary>Aprueba una solicitud pendiente (las vacaciones vuelven a validar el saldo).</summary>
    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<LeaveRequestDto>> Approve(int id, [FromBody] ReviewLeaveRequestDto dto)
        => Ok(await _service.ApproveAsync(id, Reviewer, dto.Comment));

    /// <summary>Rechaza una solicitud pendiente (el comentario es obligatorio).</summary>
    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<LeaveRequestDto>> Reject(int id, [FromBody] ReviewLeaveRequestDto dto)
        => Ok(await _service.RejectAsync(id, Reviewer, dto.Comment));
}

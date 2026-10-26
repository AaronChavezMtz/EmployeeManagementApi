using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")] // RH; el rol Employee solo accede a /api/me
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service)
    {
        _service = service;
    }

    /// <summary>Indicadores generales: plantilla total/activa, rotación y tendencia de contrataciones (últimos 12 meses).</summary>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(DashboardKpisDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardKpisDto>> GetKpis()
        => Ok(await _service.GetKpisAsync());
}

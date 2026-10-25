using System.Security.Claims;
using EmployeeManagementApi.Common;
using EmployeeManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace EmployeeManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DemoController : ControllerBase
{
    private readonly IDemoService _service;
    private readonly DemoSettings _demo;

    public DemoController(IDemoService service, IOptions<DemoSettings> demoOptions)
    {
        _service = service;
        _demo = demoOptions.Value;
    }

    private string CurrentUsername => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Indica si el usuario actual puede reiniciar la demo (para mostrar u ocultar el botón).</summary>
    [HttpGet]
    public ActionResult<object> GetStatus() => Ok(new { canReset = _demo.CanReset(CurrentUsername) });

    /// <summary>Reinicia los datos de la demo (solo con Demo:Enabled = true y para InitialAdmin/SuperAdmin).</summary>
    [HttpPost("reset")]
    [EnableRateLimiting("demo-reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Reset()
    {
        // Sin modo demo el endpoint no existe: evita borrar datos reales por error.
        if (!_demo.Enabled)
            return NotFound();

        // Los admins públicos de la demo (p. ej. adminprueba) no pueden reiniciarla.
        if (!_demo.CanReset(CurrentUsername))
            return Forbid();

        await _service.ResetAsync();
        return NoContent();
    }
}

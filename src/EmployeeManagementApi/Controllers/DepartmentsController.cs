using EmployeeManagementApi.Common;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _service;

    public DepartmentsController(IDepartmentService service)
    {
        _service = service;
    }

    /// <summary>Lista todos los departamentos con el conteo de empleados activos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>Obtiene un departamento por id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> GetById(int id)
        => Ok(await _service.GetByIdAsync(id));

    /// <summary>Crea un nuevo departamento.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Actualiza un departamento existente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DepartmentDto>> Update(int id, [FromBody] UpdateDepartmentDto dto)
        => Ok(await _service.UpdateAsync(id, dto));

    /// <summary>Elimina un departamento (solo si no tiene empleados asignados).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Reporte por departamento (empleados, activos, salario promedio y nómina total) usando la vista vw_DepartmentSummary.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<DepartmentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DepartmentSummaryDto>>> GetSummary()
        => Ok(await _service.GetSummaryAsync());
}

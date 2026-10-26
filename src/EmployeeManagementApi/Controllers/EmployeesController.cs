using EmployeeManagementApi.Common;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize] // requiere JWT válido para cualquier endpoint de este controller
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _service;

    public EmployeesController(IEmployeeService service)
    {
        _service = service;
    }

    /// <summary>Obtiene empleados con búsqueda, filtros, orden y paginación.</summary>
    /// <param name="query">Parámetros de búsqueda (search, departmentId, isActive, minSalary, maxSalary, sortBy, page, pageSize, etc.)</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<EmployeeDto>>> GetAll([FromQuery] EmployeeQueryParameters query)
    {
        var result = await _service.GetAllAsync(query);
        return Ok(result);
    }

    /// <summary>Obtiene un empleado por su id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetById(int id)
    {
        var employee = await _service.GetByIdAsync(id);
        return Ok(employee);
    }

    /// <summary>Crea un nuevo empleado.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] CreateEmployeeDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Actualiza un empleado existente.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> Update(int id, [FromBody] UpdateEmployeeDto dto)
    {
        var updated = await _service.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Da de baja a un empleado (baja lógica) con fecha y motivo.</summary>
    [HttpPost("{id:int}/terminate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDto>> Terminate(int id, [FromBody] TerminateEmployeeDto dto)
        => Ok(await _service.TerminateAsync(id, dto));

    /// <summary>Recontrata a un empleado dado de baja (reinicia su fecha de contratación).</summary>
    [HttpPost("{id:int}/rehire")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDto>> Rehire(int id, [FromBody] RehireEmployeeDto dto)
        => Ok(await _service.RehireAsync(id, dto));

    /// <summary>Bitácora de cambios del empleado (altas, cambios de departamento, salario, estado).</summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeHistoryDto>>> GetHistory(int id)
    {
        var history = await _service.GetHistoryAsync(id);
        return Ok(history);
    }
}

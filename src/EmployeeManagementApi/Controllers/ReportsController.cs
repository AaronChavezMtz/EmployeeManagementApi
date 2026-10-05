using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _service;

    public ReportsController(IReportService service)
    {
        _service = service;
    }

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Exporta el listado de empleados (con los mismos filtros de búsqueda) a Excel.</summary>
    [HttpGet("employees/excel")]
    public async Task<IActionResult> ExportEmployeesExcel([FromQuery] EmployeeQueryParameters query)
    {
        var bytes = await _service.ExportEmployeesToExcelAsync(query);
        return File(bytes, XlsxContentType, $"empleados_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>Exporta el resumen por departamento a Excel.</summary>
    [HttpGet("departments/excel")]
    public async Task<IActionResult> ExportDepartmentsExcel()
    {
        var bytes = await _service.ExportDepartmentSummaryToExcelAsync();
        return File(bytes, XlsxContentType, $"resumen_departamentos_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>Exporta el resumen por departamento a PDF.</summary>
    [HttpGet("departments/pdf")]
    public async Task<IActionResult> ExportDepartmentsPdf()
    {
        var bytes = await _service.ExportDepartmentSummaryToPdfAsync();
        return File(bytes, "application/pdf", $"resumen_departamentos_{DateTime.Now:yyyyMMdd}.pdf");
    }
}

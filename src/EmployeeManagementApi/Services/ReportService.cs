using ClosedXML.Excel;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EmployeeManagementApi.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _context;
    private readonly IEmployeeService _employeeService;

    public ReportService(AppDbContext context, IEmployeeService employeeService)
    {
        _context = context;
        _employeeService = employeeService;
    }

    public async Task<byte[]> ExportEmployeesToExcelAsync(EmployeeQueryParameters query)
    {
        // Reutiliza los mismos filtros que la pantalla de empleados, pero sin límite de página
        // (hasta un tope razonable) para que el reporte refleje lo que el usuario está viendo.
        query.PageNumber = 1;
        query.PageSize = 5000;
        var result = await _employeeService.GetAllAsync(query);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Empleados");

        string[] headers = { "Nombre", "Apellido", "Correo", "Teléfono", "Puesto", "Departamento", "Salario", "Fecha de contratación", "Estado" };
        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
            sheet.Cell(1, col + 1).Style.Font.Bold = true;
            sheet.Cell(1, col + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF4FF");
        }

        var row = 2;
        foreach (var e in result.Items)
        {
            sheet.Cell(row, 1).Value = e.FirstName;
            sheet.Cell(row, 2).Value = e.LastName;
            sheet.Cell(row, 3).Value = e.Email;
            sheet.Cell(row, 4).Value = e.Phone ?? string.Empty;
            sheet.Cell(row, 5).Value = e.Position;
            sheet.Cell(row, 6).Value = e.DepartmentName ?? string.Empty;
            sheet.Cell(row, 7).Value = e.Salary;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Cell(row, 8).Value = e.HireDate.ToString("yyyy-MM-dd");
            sheet.Cell(row, 9).Value = e.IsActive ? "Activo" : "Inactivo";
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportDepartmentSummaryToExcelAsync()
    {
        var summary = await _context.DepartmentSummaries.AsNoTracking().ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Resumen por departamento");

        string[] headers = { "Departamento", "Total empleados", "Activos", "Salario promedio", "Nómina total" };
        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
            sheet.Cell(1, col + 1).Style.Font.Bold = true;
            sheet.Cell(1, col + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF4FF");
        }

        var row = 2;
        foreach (var d in summary)
        {
            sheet.Cell(row, 1).Value = d.DepartmentName;
            sheet.Cell(row, 2).Value = d.TotalEmployees;
            sheet.Cell(row, 3).Value = d.ActiveEmployees;
            sheet.Cell(row, 4).Value = d.AverageSalary;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Cell(row, 5).Value = d.TotalPayroll;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportDepartmentSummaryToPdfAsync()
    {
        var summary = await _context.DepartmentSummaries.AsNoTracking().ToListAsync();
        var generatedAt = DateTime.Now;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri));

                page.Header().Column(col =>
                {
                    col.Item().Text("Reporte de empleados por departamento").FontSize(18).Bold();
                    col.Item().Text($"Generado el {generatedAt:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(15).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        string[] headers = { "Departamento", "Total", "Activos", "Salario prom.", "Nómina total" };
                        foreach (var h in headers)
                        {
                            header.Cell().Background(Colors.Blue.Lighten4).Padding(5)
                                .Text(h).Bold().FontSize(9);
                        }
                    });

                    foreach (var d in summary)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(d.DepartmentName);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(d.TotalEmployees.ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(d.ActiveEmployees.ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(d.AverageSalary.ToString("C0"));
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(d.TotalPayroll.ToString("C0"));
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Sistema de Gestión de Empleados — generado automáticamente").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }
}

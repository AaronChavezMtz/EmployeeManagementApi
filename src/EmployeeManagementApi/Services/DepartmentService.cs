using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Services;

public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _context;

    public DepartmentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<DepartmentDto>> GetAllAsync()
    {
        return await _context.Departments
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                EmployeeCount = d.Employees.Count(e => e.IsActive)
            })
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<DepartmentDto> GetByIdAsync(int id)
    {
        var department = await _context.Departments
            .Where(d => d.Id == id)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                EmployeeCount = d.Employees.Count(e => e.IsActive)
            })
            .FirstOrDefaultAsync();

        if (department is null)
            throw new NotFoundException($"No se encontró el departamento con id {id}");

        return department;
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto)
    {
        var nameInUse = await _context.Departments.AnyAsync(d => d.Name.ToLower() == dto.Name.ToLower());
        if (nameInUse)
            throw new BusinessRuleException($"Ya existe un departamento llamado '{dto.Name}'");

        var department = new Department
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync();

        return new DepartmentDto { Id = department.Id, Name = department.Name, Description = department.Description, EmployeeCount = 0 };
    }

    public async Task<DepartmentDto> UpdateAsync(int id, UpdateDepartmentDto dto)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department is null)
            throw new NotFoundException($"No se encontró el departamento con id {id}");

        var nameInUse = await _context.Departments.AnyAsync(d => d.Id != id && d.Name.ToLower() == dto.Name.ToLower());
        if (nameInUse)
            throw new BusinessRuleException($"Ya existe otro departamento llamado '{dto.Name}'");

        department.Name = dto.Name.Trim();
        department.Description = dto.Description?.Trim();
        await _context.SaveChangesAsync();

        var employeeCount = await _context.Employees.CountAsync(e => e.DepartmentId == id && e.IsActive);
        return new DepartmentDto { Id = department.Id, Name = department.Name, Description = department.Description, EmployeeCount = employeeCount };
    }

    public async Task DeleteAsync(int id)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department is null)
            throw new NotFoundException($"No se encontró el departamento con id {id}");

        var hasEmployees = await _context.Employees.AnyAsync(e => e.DepartmentId == id);
        if (hasEmployees)
            throw new BusinessRuleException("No se puede eliminar un departamento que tiene empleados asignados");

        _context.Departments.Remove(department);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<DepartmentSummaryDto>> GetSummaryAsync()
    {
        var rows = await _context.DepartmentSummaries.AsNoTracking().ToListAsync();

        return rows.Select(r => new DepartmentSummaryDto
        {
            DepartmentId = r.DepartmentId,
            DepartmentName = r.DepartmentName,
            TotalEmployees = r.TotalEmployees,
            ActiveEmployees = r.ActiveEmployees,
            AverageSalary = r.AverageSalary,
            TotalPayroll = r.TotalPayroll
        });
    }
}

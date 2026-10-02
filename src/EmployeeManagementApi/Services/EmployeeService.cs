using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Services;

public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(AppDbContext context, ILogger<EmployeeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResultDto<EmployeeDto>> GetAllAsync(EmployeeQueryParameters q)
    {
        var query = _context.Employees.Include(e => e.Department).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLower();
            query = query.Where(e =>
                e.FirstName.ToLower().Contains(term) ||
                e.LastName.ToLower().Contains(term) ||
                e.Email.ToLower().Contains(term) ||
                e.Position.ToLower().Contains(term));
        }

        if (q.DepartmentId.HasValue)
            query = query.Where(e => e.DepartmentId == q.DepartmentId.Value);

        if (q.IsActive.HasValue)
            query = query.Where(e => e.IsActive == q.IsActive.Value);

        if (q.MinSalary.HasValue)
            query = query.Where(e => e.Salary >= q.MinSalary.Value);

        if (q.MaxSalary.HasValue)
            query = query.Where(e => e.Salary <= q.MaxSalary.Value);

        if (q.HiredAfter.HasValue)
            query = query.Where(e => e.HireDate >= q.HiredAfter.Value);

        if (q.HiredBefore.HasValue)
            query = query.Where(e => e.HireDate <= q.HiredBefore.Value);

        query = (q.SortBy?.ToLower()) switch
        {
            "salary" => q.Descending ? query.OrderByDescending(e => e.Salary) : query.OrderBy(e => e.Salary),
            "hiredate" => q.Descending ? query.OrderByDescending(e => e.HireDate) : query.OrderBy(e => e.HireDate),
            "department" => q.Descending ? query.OrderByDescending(e => e.Department!.Name) : query.OrderBy(e => e.Department!.Name),
            _ => q.Descending ? query.OrderByDescending(e => e.LastName) : query.OrderBy(e => e.LastName),
        };

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(e => MapToDto(e))
            .ToListAsync();

        return new PagedResultDto<EmployeeDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = q.PageNumber,
            PageSize = q.PageSize
        };
    }

    public async Task<EmployeeDto> GetByIdAsync(int id)
    {
        var employee = await _context.Employees.Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee is null)
            throw new NotFoundException($"No se encontró el empleado con id {id}");

        return MapToDto(employee);
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto)
    {
        var departmentExists = await _context.Departments.AnyAsync(d => d.Id == dto.DepartmentId);
        if (!departmentExists)
            throw new BusinessRuleException($"El departamento con id {dto.DepartmentId} no existe");

        var emailInUse = await _context.Employees.AnyAsync(e => e.Email.ToLower() == dto.Email.ToLower());
        if (emailInUse)
            throw new BusinessRuleException($"Ya existe un empleado registrado con el correo {dto.Email}");

        var employee = new Employee
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = dto.Phone,
            Position = dto.Position.Trim(),
            Salary = dto.Salary,
            HireDate = dto.HireDate,
            BirthDate = dto.BirthDate,
            DepartmentId = dto.DepartmentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Empleado creado: {Email} (Id {Id})", employee.Email, employee.Id);

        await _context.Entry(employee).Reference(e => e.Department).LoadAsync();
        return MapToDto(employee);
    }

    public async Task<EmployeeDto> UpdateAsync(int id, UpdateEmployeeDto dto)
    {
        var employee = await _context.Employees.Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee is null)
            throw new NotFoundException($"No se encontró el empleado con id {id}");

        var departmentExists = await _context.Departments.AnyAsync(d => d.Id == dto.DepartmentId);
        if (!departmentExists)
            throw new BusinessRuleException($"El departamento con id {dto.DepartmentId} no existe");

        var emailInUse = await _context.Employees
            .AnyAsync(e => e.Id != id && e.Email.ToLower() == dto.Email.ToLower());
        if (emailInUse)
            throw new BusinessRuleException($"Ya existe otro empleado registrado con el correo {dto.Email}");

        employee.FirstName = dto.FirstName.Trim();
        employee.LastName = dto.LastName.Trim();
        employee.Email = dto.Email.Trim().ToLower();
        employee.Phone = dto.Phone;
        employee.Position = dto.Position.Trim();
        employee.Salary = dto.Salary;
        employee.BirthDate = dto.BirthDate;
        employee.DepartmentId = dto.DepartmentId;
        employee.IsActive = dto.IsActive;
        employee.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (employee.Department?.Id != dto.DepartmentId)
            await _context.Entry(employee).Reference(e => e.Department).LoadAsync();

        return MapToDto(employee);
    }

    public async Task DeleteAsync(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee is null)
            throw new NotFoundException($"No se encontró el empleado con id {id}");

        // Baja lógica: conserva historial en lugar de borrar el registro físicamente.
        employee.IsActive = false;
        employee.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Empleado dado de baja: Id {Id}", id);
    }

    public async Task<IEnumerable<EmployeeDto>> SearchWithStoredProcedureAsync(
        string? searchTerm, int? departmentId, bool? isActive)
    {
        var searchParam = new SqlParameter("@SearchTerm", (object?)searchTerm ?? DBNull.Value);
        var deptParam = new SqlParameter("@DepartmentId", (object?)departmentId ?? DBNull.Value);
        var activeParam = new SqlParameter("@IsActive", (object?)isActive ?? DBNull.Value);

        var results = await _context.Employees
            .FromSqlRaw("EXEC dbo.sp_SearchEmployees @SearchTerm, @DepartmentId, @IsActive",
                searchParam, deptParam, activeParam)
            .Include(e => e.Department)
            .AsNoTracking()
            .ToListAsync();

        return results.Select(MapToDto);
    }

    private static EmployeeDto MapToDto(Employee e) => new()
    {
        Id = e.Id,
        FirstName = e.FirstName,
        LastName = e.LastName,
        Email = e.Email,
        Phone = e.Phone,
        Position = e.Position,
        Salary = e.Salary,
        HireDate = e.HireDate,
        BirthDate = e.BirthDate,
        IsActive = e.IsActive,
        DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.Name
    };
}

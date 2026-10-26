using System.Security.Claims;
using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Services;

public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<EmployeeService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmployeeService(AppDbContext context, ILogger<EmployeeService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    private string CurrentUsername =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub")
        ?? "sistema";

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

        if (dto.HireDate.Date > DateTime.UtcNow.Date)
            throw new BusinessRuleException("La fecha de contratación no puede ser futura");

        if (dto.HireDate.Date < dto.BirthDate.Date.AddYears(18))
            throw new BusinessRuleException("El empleado debía tener al menos 18 años en la fecha de contratación");

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

        _context.EmployeeHistories.Add(new EmployeeHistory
        {
            EmployeeId = employee.Id,
            ChangeType = HistoryChangeType.Created,
            NewValue = $"{employee.FirstName} {employee.LastName} contratado como {employee.Position}",
            ChangedBy = CurrentUsername,
            ChangedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Empleado creado: {Email} (Id {Id}) por {User}", employee.Email, employee.Id, CurrentUsername);

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

        var historyEntries = new List<EmployeeHistory>();
        var now = DateTime.UtcNow;

        if (employee.DepartmentId != dto.DepartmentId)
        {
            var oldDeptName = employee.Department?.Name ?? employee.DepartmentId.ToString();
            var newDept = await _context.Departments.FindAsync(dto.DepartmentId);
            historyEntries.Add(new EmployeeHistory
            {
                EmployeeId = id, ChangeType = HistoryChangeType.DepartmentChanged,
                OldValue = oldDeptName, NewValue = newDept?.Name, ChangedBy = CurrentUsername, ChangedAtUtc = now
            });
        }

        if (employee.Salary != dto.Salary)
        {
            historyEntries.Add(new EmployeeHistory
            {
                EmployeeId = id, ChangeType = HistoryChangeType.SalaryChanged,
                OldValue = employee.Salary.ToString("F2"), NewValue = dto.Salary.ToString("F2"),
                ChangedBy = CurrentUsername, ChangedAtUtc = now
            });
        }

        employee.FirstName = dto.FirstName.Trim();
        employee.LastName = dto.LastName.Trim();
        employee.Email = dto.Email.Trim().ToLower();
        employee.Phone = dto.Phone;
        employee.Position = dto.Position.Trim();
        employee.Salary = dto.Salary;
        employee.BirthDate = dto.BirthDate;
        employee.DepartmentId = dto.DepartmentId;
        employee.UpdatedAt = now;

        if (historyEntries.Count == 0)
        {
            historyEntries.Add(new EmployeeHistory
            {
                EmployeeId = id, ChangeType = HistoryChangeType.Updated,
                NewValue = "Datos generales actualizados", ChangedBy = CurrentUsername, ChangedAtUtc = now
            });
        }
        _context.EmployeeHistories.AddRange(historyEntries);

        await _context.SaveChangesAsync();

        if (employee.Department?.Id != dto.DepartmentId)
            await _context.Entry(employee).Reference(e => e.Department).LoadAsync();

        return MapToDto(employee);
    }

    public async Task<EmployeeDto> TerminateAsync(int id, TerminateEmployeeDto dto)
    {
        var employee = await FindWithDepartmentAsync(id);
        if (!employee.IsActive)
            throw new BusinessRuleException("El empleado ya está dado de baja");

        var date = dto.TerminationDate.Date;
        if (date > DateTime.UtcNow.Date)
            throw new BusinessRuleException("La fecha de baja no puede ser futura");
        if (date < employee.HireDate.Date)
            throw new BusinessRuleException("La fecha de baja no puede ser anterior a la de contratación");

        // Baja lógica: conserva el registro y su historial.
        employee.IsActive = false;
        employee.TerminationDate = date;
        employee.TerminationReason = dto.Reason!.Value;
        employee.UpdatedAt = DateTime.UtcNow;

        // Un ex empleado ya no debe entrar al portal (el JWT se revalida en cada petición).
        var linkedUsers = await _context.Users.Where(u => u.EmployeeId == id).ToListAsync();
        linkedUsers.ForEach(u => u.IsActive = false);

        _context.EmployeeHistories.Add(new EmployeeHistory
        {
            EmployeeId = id,
            ChangeType = HistoryChangeType.Deactivated,
            NewValue = $"{ReasonLabel(employee.TerminationReason.Value)} el {date:yyyy-MM-dd}",
            ChangedBy = CurrentUsername,
            ChangedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Empleado dado de baja: Id {Id} ({Reason}) por {User}", id, employee.TerminationReason, CurrentUsername);
        return MapToDto(employee);
    }

    public async Task<EmployeeDto> RehireAsync(int id, RehireEmployeeDto dto)
    {
        var employee = await FindWithDepartmentAsync(id);
        if (employee.IsActive)
            throw new BusinessRuleException("El empleado ya está activo");

        var date = dto.HireDate.Date;
        if (date > DateTime.UtcNow.Date)
            throw new BusinessRuleException("La fecha de recontratación no puede ser futura");
        if (employee.TerminationDate is { } terminated && date < terminated.Date)
            throw new BusinessRuleException("La fecha de recontratación no puede ser anterior a la baja");

        // La antigüedad se reinicia: la baja anterior queda registrada en el historial.
        var previous = employee.TerminationDate;
        employee.IsActive = true;
        employee.HireDate = date;
        employee.TerminationDate = null;
        employee.TerminationReason = null;
        employee.UpdatedAt = DateTime.UtcNow;

        _context.EmployeeHistories.Add(new EmployeeHistory
        {
            EmployeeId = id,
            ChangeType = HistoryChangeType.Activated,
            OldValue = previous is null ? null : $"Baja el {previous:yyyy-MM-dd}",
            NewValue = $"Recontratado el {date:yyyy-MM-dd}",
            ChangedBy = CurrentUsername,
            ChangedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Empleado recontratado: Id {Id} por {User}", id, CurrentUsername);
        return MapToDto(employee);
    }

    private async Task<Employee> FindWithDepartmentAsync(int id) =>
        await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == id)
        ?? throw new NotFoundException($"No se encontró el empleado con id {id}");

    private static string ReasonLabel(TerminationReason reason) => reason switch
    {
        TerminationReason.Resignation => "Renuncia",
        TerminationReason.Dismissal => "Despido",
        TerminationReason.ContractEnd => "Término de contrato",
        _ => "Baja"
    };

    public async Task<EmployeeDto> UpdatePhoneAsync(int id, string? phone)
    {
        var employee = await FindWithDepartmentAsync(id);
        var newPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (employee.Phone == newPhone)
            return MapToDto(employee);

        _context.EmployeeHistories.Add(new EmployeeHistory
        {
            EmployeeId = id, ChangeType = HistoryChangeType.Updated,
            OldValue = employee.Phone, NewValue = $"Teléfono actualizado por el empleado: {newPhone ?? "(vacío)"}",
            ChangedBy = CurrentUsername, ChangedAtUtc = DateTime.UtcNow
        });
        employee.Phone = newPhone;
        employee.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return MapToDto(employee);
    }

    public async Task<IEnumerable<EmployeeHistoryDto>> GetHistoryAsync(int employeeId)
    {
        var exists = await _context.Employees.AnyAsync(e => e.Id == employeeId);
        if (!exists)
            throw new NotFoundException($"No se encontró el empleado con id {employeeId}");

        return await _context.EmployeeHistories
            .Where(h => h.EmployeeId == employeeId)
            .OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => new EmployeeHistoryDto
            {
                Id = h.Id,
                ChangeType = h.ChangeType,
                OldValue = h.OldValue,
                NewValue = h.NewValue,
                ChangedBy = h.ChangedBy,
                ChangedAtUtc = h.ChangedAtUtc
            })
            .ToListAsync();
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
        TerminationDate = e.TerminationDate,
        TerminationReason = e.TerminationReason?.ToString(),
        DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.Name
    };
}

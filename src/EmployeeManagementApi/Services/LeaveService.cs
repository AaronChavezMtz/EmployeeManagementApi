using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Services;

public class LeaveService : ILeaveService
{
    private const int MaxRequestDays = 60;      // días naturales por solicitud
    private const int SickLeaveBackdateDays = 30; // una incapacidad se puede registrar después

    private readonly AppDbContext _context;
    private readonly ILogger<LeaveService> _logger;

    public LeaveService(AppDbContext context, ILogger<LeaveService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ---------- Reglas puras (testeables sin base de datos) ----------

    // Días de vacaciones por años de servicio cumplidos (LFT art. 76, reforma 2023).
    public static int VacationDaysFor(int yearsOfService) => yearsOfService switch
    {
        < 1 => 0, // el derecho nace al cumplir el primer año
        <= 5 => 10 + 2 * yearsOfService,           // 12, 14, 16, 18, 20
        _ => 22 + 2 * ((yearsOfService - 6) / 5)  // 6-10: 22, 11-15: 24, ...
    };

    public static int BusinessDays(DateTime start, DateTime end)
    {
        var days = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                days++;
        return days;
    }

    // Periodo de aniversario que contiene "today": [último aniversario, próximo aniversario).
    public static (int Years, DateTime Start, DateTime End) AnniversaryPeriod(DateTime hireDate, DateTime today)
    {
        var years = today.Year - hireDate.Year;
        if (hireDate.Date.AddYears(years) > today.Date) years--;
        years = Math.Max(years, 0);
        var start = hireDate.Date.AddYears(years);
        return (years, start, start.AddYears(1));
    }

    // ---------- Portal del empleado ----------

    public async Task<IEnumerable<LeaveRequestDto>> GetForEmployeeAsync(int employeeId) =>
        await Query().Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.StartDate)
            .Select(ToDto)
            .ToListAsync();

    public async Task<VacationBalanceDto> GetBalanceAsync(int employeeId)
    {
        var employee = await _context.Employees.FindAsync(employeeId)
            ?? throw new NotFoundException($"No se encontró el empleado con id {employeeId}");
        return await BalanceForAsync(employee, excludeRequestId: null);
    }

    public async Task<LeaveRequestDto> CreateAsync(int employeeId, CreateLeaveRequestDto dto)
    {
        var employee = await _context.Employees.FindAsync(employeeId)
            ?? throw new NotFoundException($"No se encontró el empleado con id {employeeId}");
        if (!employee.IsActive)
            throw new BusinessRuleException("Un empleado dado de baja no puede solicitar ausencias");

        var type = dto.Type!.Value;
        var start = dto.StartDate.Date;
        var end = dto.EndDate.Date;
        var today = DateTime.UtcNow.Date;

        if (end < start)
            throw new BusinessRuleException("La fecha de fin no puede ser anterior a la de inicio");
        if ((end - start).TotalDays + 1 > MaxRequestDays)
            throw new BusinessRuleException($"Una solicitud no puede abarcar más de {MaxRequestDays} días");
        var earliest = type == LeaveType.Sick ? today.AddDays(-SickLeaveBackdateDays) : today;
        if (start < earliest)
            throw new BusinessRuleException(type == LeaveType.Sick
                ? $"Una incapacidad solo se puede registrar hasta {SickLeaveBackdateDays} días atrás"
                : "La solicitud no puede empezar en una fecha pasada");

        var days = BusinessDays(start, end);
        if (days == 0)
            throw new BusinessRuleException("El periodo no incluye días hábiles");

        var overlaps = await _context.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == employeeId &&
            (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved) &&
            r.StartDate <= end && r.EndDate >= start);
        if (overlaps)
            throw new BusinessRuleException("Ya tienes una solicitud pendiente o aprobada en esas fechas");

        if (type == LeaveType.Vacation)
            await EnsureBalanceAsync(employee, days, excludeRequestId: null);

        var request = new LeaveRequest
        {
            EmployeeId = employeeId, Type = type, StartDate = start, EndDate = end, Days = days,
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.LeaveRequests.Add(request);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Solicitud de ausencia {Id} ({Type}, {Days} días) del empleado {EmployeeId}", request.Id, type, days, employeeId);
        return await GetDtoAsync(request.Id);
    }

    public async Task<LeaveRequestDto> CancelAsync(int employeeId, int requestId)
    {
        var request = await _context.LeaveRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.EmployeeId == employeeId)
            ?? throw new NotFoundException($"No se encontró la solicitud {requestId}");

        var canCancel = request.Status == LeaveStatus.Pending
            || (request.Status == LeaveStatus.Approved && request.StartDate > DateTime.UtcNow.Date);
        if (!canCancel)
            throw new BusinessRuleException("Solo se puede cancelar una solicitud pendiente o una aprobada que aún no empieza");

        request.Status = LeaveStatus.Cancelled;
        await _context.SaveChangesAsync();
        return await GetDtoAsync(request.Id);
    }

    // ---------- RH ----------

    public async Task<IEnumerable<LeaveRequestDto>> GetAllAsync(LeaveStatus? status)
    {
        var query = Query();
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        // Pendientes primero en orden de llegada; el resto, lo más reciente primero.
        return await query
            .OrderBy(r => r.Status == LeaveStatus.Pending ? 0 : 1)
            .ThenBy(r => r.Status == LeaveStatus.Pending ? r.CreatedAtUtc : DateTime.MaxValue)
            .ThenByDescending(r => r.StartDate)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<LeaveRequestDto> ApproveAsync(int requestId, string reviewer, string? comment)
    {
        var request = await PendingRequestAsync(requestId);

        // El saldo pudo cambiar desde que se pidió (otra solicitud aprobada, recontratación...).
        if (request.Type == LeaveType.Vacation)
            await EnsureBalanceAsync(request.Employee!, request.Days, excludeRequestId: request.Id);

        Review(request, LeaveStatus.Approved, reviewer, comment);
        await _context.SaveChangesAsync();
        return await GetDtoAsync(request.Id);
    }

    public async Task<LeaveRequestDto> RejectAsync(int requestId, string reviewer, string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new BusinessRuleException("Indica el motivo del rechazo");

        var request = await PendingRequestAsync(requestId);
        Review(request, LeaveStatus.Rejected, reviewer, comment);
        await _context.SaveChangesAsync();
        return await GetDtoAsync(request.Id);
    }

    // ---------- Auxiliares ----------

    private async Task<VacationBalanceDto> BalanceForAsync(Employee employee, int? excludeRequestId)
    {
        var (years, start, end) = AnniversaryPeriod(employee.HireDate, DateTime.UtcNow.Date);

        var used = await _context.LeaveRequests
            .Where(r => r.EmployeeId == employee.Id && r.Type == LeaveType.Vacation && r.Id != excludeRequestId
                        && r.StartDate >= start && r.StartDate < end
                        && (r.Status == LeaveStatus.Approved || r.Status == LeaveStatus.Pending))
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Days = g.Sum(r => r.Days) })
            .ToListAsync();

        var approved = used.FirstOrDefault(u => u.Status == LeaveStatus.Approved)?.Days ?? 0;
        var pending = used.FirstOrDefault(u => u.Status == LeaveStatus.Pending)?.Days ?? 0;
        var entitled = VacationDaysFor(years);

        return new VacationBalanceDto
        {
            YearsOfService = years, PeriodStart = start, PeriodEnd = end,
            EntitledDays = entitled, ApprovedDays = approved, PendingDays = pending,
            AvailableDays = Math.Max(entitled - approved - pending, 0)
        };
    }

    private async Task EnsureBalanceAsync(Employee employee, int days, int? excludeRequestId)
    {
        var balance = await BalanceForAsync(employee, excludeRequestId);
        if (balance.EntitledDays == 0)
            throw new BusinessRuleException("Las vacaciones se generan al cumplir el primer año de servicio");
        if (days > balance.AvailableDays)
            throw new BusinessRuleException($"Solo quedan {balance.AvailableDays} días de vacaciones disponibles en el periodo actual");
    }

    private async Task<LeaveRequest> PendingRequestAsync(int requestId)
    {
        var request = await _context.LeaveRequests.Include(r => r.Employee).FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException($"No se encontró la solicitud {requestId}");
        if (request.Status != LeaveStatus.Pending)
            throw new BusinessRuleException("La solicitud ya fue revisada o cancelada");
        return request;
    }

    private static void Review(LeaveRequest request, LeaveStatus status, string reviewer, string? comment)
    {
        request.Status = status;
        request.ReviewedBy = reviewer;
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ReviewComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    private IQueryable<LeaveRequest> Query() => _context.LeaveRequests.AsNoTracking().Include(r => r.Employee);

    private static readonly System.Linq.Expressions.Expression<Func<LeaveRequest, LeaveRequestDto>> ToDto = r => new LeaveRequestDto
    {
        Id = r.Id,
        EmployeeId = r.EmployeeId,
        EmployeeName = r.Employee!.FirstName + " " + r.Employee.LastName,
        Type = r.Type,
        StartDate = r.StartDate,
        EndDate = r.EndDate,
        Days = r.Days,
        Reason = r.Reason,
        Status = r.Status,
        ReviewedBy = r.ReviewedBy,
        ReviewedAtUtc = r.ReviewedAtUtc,
        ReviewComment = r.ReviewComment,
        CreatedAtUtc = r.CreatedAtUtc
    };

    // La misma proyección en memoria, para devolver una solicitud recién guardada.
    private static readonly Func<LeaveRequest, LeaveRequestDto> ToDtoInMemory = ToDto.Compile();

    private async Task<LeaveRequestDto> GetDtoAsync(int id) => ToDtoInMemory(await Query().FirstAsync(r => r.Id == id));
}

using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EmployeeManagementApi.Services;

// Restaura la demo pública a un estado conocido: borra empleados, historial, departamentos
// y usuarios no protegidos, y vuelve a sembrar datos ficticios.
public class DemoService : IDemoService
{
    private readonly AppDbContext _context;
    private readonly DemoSettings _demo;
    private readonly ILogger<DemoService> _logger;

    public DemoService(AppDbContext context, IOptions<DemoSettings> demoOptions, ILogger<DemoService> logger)
    {
        _context = context;
        _demo = demoOptions.Value;
        _logger = logger;
    }

    public async Task ResetAsync()
    {
        _context.LeaveRequests.RemoveRange(await _context.LeaveRequests.ToListAsync());
        _context.EmployeeHistories.RemoveRange(await _context.EmployeeHistories.ToListAsync());
        _context.Employees.RemoveRange(await _context.Employees.ToListAsync());
        _context.Departments.RemoveRange(await _context.Departments.ToListAsync());

        var users = await _context.Users.ToListAsync();
        _context.Users.RemoveRange(users.Where(u => !_demo.IsProtected(u.Username)));
        var protectedEmployeeUsers = users.Where(u => _demo.IsProtected(u.Username) && u.Role == UserRole.Employee).ToList();
        protectedEmployeeUsers.ForEach(u => u.EmployeeId = null); // se vuelven a vincular tras sembrar

        // Primero los borrados: los nombres de departamento son únicos y se vuelven a insertar.
        await _context.SaveChangesAsync();

        var departments = new[]
        {
            new Department { Name = "Tecnología", Description = "Desarrollo de software e infraestructura" },
            new Department { Name = "Recursos Humanos", Description = "Gestión de personal y nómina" },
            new Department { Name = "Ventas", Description = "Comercialización y atención a clientes" },
            new Department { Name = "Finanzas", Description = "Contabilidad y control financiero" }
        };
        _context.Departments.AddRange(departments);

        var today = DateTime.UtcNow.Date;
        Employee Emp(string first, string last, string position, decimal salary, int monthsAgo, int birthYear, Department dept,
            int? terminatedMonthsAgo = null, TerminationReason? reason = null) => new()
        {
            FirstName = first,
            LastName = last,
            Email = $"{first}.{last}@example.com".ToLowerInvariant(),
            Position = position,
            Salary = salary,
            HireDate = today.AddMonths(-monthsAgo),
            BirthDate = new DateTime(birthYear, 6, 15),
            IsActive = terminatedMonthsAgo is null,
            TerminationDate = terminatedMonthsAgo is { } t ? today.AddMonths(-t) : null,
            TerminationReason = reason,
            Department = dept
        };

        var employees = new[]
        {
            Emp("Ana", "Garcia", "Desarrolladora Backend", 38000, 30, 1992, departments[0]),
            Emp("Luis", "Hernandez", "Desarrollador Frontend", 34000, 11, 1995, departments[0]),
            Emp("Sofia", "Ramirez", "Ingeniera DevOps", 42000, 7, 1990, departments[0]),
            Emp("Diego", "Torres", "QA Tester", 26000, 3, 1998, departments[0]),
            Emp("Mariana", "Lopez", "Reclutadora", 22000, 20, 1993, departments[1]),
            Emp("Jorge", "Martinez", "Analista de Nómina", 24000, 9, 1988, departments[1], terminatedMonthsAgo: 2, reason: TerminationReason.Resignation),
            Emp("Valeria", "Sanchez", "Ejecutiva de Ventas", 20000, 5, 1997, departments[2]),
            Emp("Carlos", "Flores", "Gerente de Ventas", 45000, 40, 1985, departments[2]),
            Emp("Fernanda", "Cruz", "Ejecutiva de Cuenta", 21000, 1, 1999, departments[2]),
            Emp("Ricardo", "Morales", "Contador", 30000, 15, 1987, departments[3]),
            Emp("Paola", "Jimenez", "Analista Financiera", 28000, 6, 1994, departments[3]),
            Emp("Miguel", "Reyes", "Auxiliar Contable", 18000, 8, 2000, departments[3], terminatedMonthsAgo: 1, reason: TerminationReason.Dismissal)
        };
        _context.Employees.AddRange(employees);
        await _context.SaveChangesAsync();

        // Las cuentas de demo con rol Employee quedan vinculadas a empleados activos sembrados,
        // así el portal del empleado siempre tiene datos que mostrar.
        var activeEmployees = employees.Where(e => e.IsActive).ToList();
        for (var i = 0; i < protectedEmployeeUsers.Count && i < activeEmployees.Count; i++)
        {
            protectedEmployeeUsers[i].EmployeeId = activeEmployees[i].Id;
            protectedEmployeeUsers[i].IsActive = true;
        }

        _context.EmployeeHistories.AddRange(employees.Select(e => new EmployeeHistory
        {
            EmployeeId = e.Id,
            ChangeType = HistoryChangeType.Created,
            NewValue = $"{e.FirstName} {e.LastName} contratado como {e.Position}",
            ChangedBy = "sistema",
            ChangedAtUtc = e.HireDate
        }));
        // Algunas solicitudes para que la bandeja de RH y el portal no estén vacíos.
        DateTime NextMonday(int weeksAhead) => today.AddDays(((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7 + 7 * weeksAhead);
        LeaveRequest Leave(Employee e, LeaveType type, DateTime start, int businessDays, LeaveStatus status, string? reason) => new()
        {
            EmployeeId = e.Id, Type = type, StartDate = start, EndDate = start.AddDays(businessDays - 1),
            Days = businessDays, Status = status, Reason = reason, CreatedAtUtc = DateTime.UtcNow,
            ReviewedBy = status == LeaveStatus.Pending ? null : "sistema",
            ReviewedAtUtc = status == LeaveStatus.Pending ? null : DateTime.UtcNow
        };
        _context.LeaveRequests.AddRange(
            Leave(employees[0], LeaveType.Vacation, NextMonday(3), 5, LeaveStatus.Pending, "Viaje familiar"),
            Leave(employees[7], LeaveType.Vacation, NextMonday(2), 3, LeaveStatus.Approved, null),
            Leave(employees[9], LeaveType.Personal, NextMonday(1), 1, LeaveStatus.Pending, "Trámite personal"),
            Leave(employees[2], LeaveType.Sick, NextMonday(0).AddDays(-7), 2, LeaveStatus.Approved, "Incapacidad IMSS"));

        _context.EmployeeHistories.AddRange(employees.Where(e => e.TerminationDate is not null).Select(e => new EmployeeHistory
        {
            EmployeeId = e.Id,
            ChangeType = HistoryChangeType.Deactivated,
            NewValue = $"Baja el {e.TerminationDate:yyyy-MM-dd}",
            ChangedBy = "sistema",
            ChangedAtUtc = e.TerminationDate!.Value
        }));
        await _context.SaveChangesAsync();

        _logger.LogWarning("Demo reiniciada: {Employees} empleados sembrados", employees.Length);
    }
}

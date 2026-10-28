using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace EmployeeManagementApi.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly DemoSettings _demo;

    public UserService(AppDbContext context, IOptions<DemoSettings> demoOptions)
    {
        _context = context;
        _demo = demoOptions.Value;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync(string currentUsername)
    {
        var users = await _context.Users.Include(u => u.Employee).OrderBy(u => u.Username).ToListAsync();
        return users.Select(u => UserDto.From(u,
            isProtected: _demo.IsProtected(u.Username),
            // La propia cuenta tampoco se edita desde la tabla (no quitarse el rol por accidente).
            canEdit: u.Username != currentUsername && _demo.CanEdit(currentUsername, u.Username)));
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserDto dto, string currentUsername)
    {
        var user = await _context.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            throw new NotFoundException($"No se encontró el usuario con id {id}");

        if (!_demo.CanEdit(currentUsername, user.Username))
            throw new BusinessRuleException("Esta cuenta está protegida y no se puede modificar");

        var role = UserRoles.Parse(dto.Role);

        // Evita que un admin se quite su propio rol o se desactive a sí mismo por accidente,
        // lo cual podría dejar al sistema sin ningún administrador activo.
        if (user.Username == currentUsername && (role != UserRole.Admin || !dto.IsActive))
            throw new BusinessRuleException("No puedes quitarte tu propio rol de administrador ni desactivar tu propia cuenta");

        user.EmployeeId = await UserLinkRules.ResolveEmployeeIdAsync(_context, role, dto.EmployeeId, ignoreUserId: user.Id);
        user.Role = role;
        user.IsActive = dto.IsActive;
        await _context.SaveChangesAsync();

        await _context.Entry(user).Reference(u => u.Employee).LoadAsync();
        return UserDto.From(user);
    }

    public async Task ChangePasswordAsync(string username, ChangePasswordDto dto)
    {
        // Las públicas de la demo las comparten todos los visitantes; las del dueño se sincronizan
        // con la configuración al arrancar, así que un cambio aquí se perdería.
        if (_demo.IsOwner(username))
            throw new BusinessRuleException("La contraseña de esta cuenta se gestiona desde la configuración del servidor");
        if (_demo.IsProtected(username))
            throw new BusinessRuleException("Las cuentas de demostración no tienen permitido cambiar la contraseña");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username)
            ?? throw new NotFoundException("No se encontró tu usuario");

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            throw new BusinessRuleException("La contraseña actual no es correcta");
        if (dto.NewPassword == dto.CurrentPassword)
            throw new BusinessRuleException("La nueva contraseña debe ser distinta de la actual");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        user.MustChangePassword = false;
        await _context.SaveChangesAsync();
    }

    // Sin caracteres ambiguos (0/O, 1/l/I) porque la contraseña se dicta o se copia a mano.
    private const string TemporaryPasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    // El usuario del portal es el correo del empleado (si cabe en los 50 caracteres del nombre de usuario).
    private static string PortalUsernameFor(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length <= 50)
            return normalized;
        var localPart = normalized.Split('@')[0];
        return localPart[..Math.Min(50, localPart.Length)];
    }

    public async Task EnsurePortalUsernameAvailableAsync(string employeeEmail)
    {
        var username = PortalUsernameFor(employeeEmail);
        var email = employeeEmail.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(u => u.Username == username || u.Email == email))
            throw new BusinessRuleException($"Ya existe un usuario con el nombre '{username}' o con ese correo; vincúlalo desde Usuarios");
    }

    public async Task<PortalAccessDto> CreatePortalAccessAsync(int employeeId)
    {
        var linkedId = await UserLinkRules.ResolveEmployeeIdAsync(_context, UserRole.Employee, employeeId);
        var employee = await _context.Employees.FindAsync(linkedId!.Value);
        await EnsurePortalUsernameAvailableAsync(employee!.Email);

        var password = RandomNumberGenerator.GetString(TemporaryPasswordAlphabet, 12);
        var user = new User
        {
            Username = PortalUsernameFor(employee.Email),
            Email = employee.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            Role = UserRole.Employee,
            EmployeeId = employee.Id,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new PortalAccessDto { Username = user.Username, TemporaryPassword = password };
    }

    public async Task<int> GetLinkedEmployeeIdAsync(string username)
    {
        var employeeId = await _context.Users
            .Where(u => u.Username == username)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();

        return employeeId ?? throw new BusinessRuleException("Tu cuenta no está vinculada a ningún empleado. Pide a RH que la vincule.");
    }
}

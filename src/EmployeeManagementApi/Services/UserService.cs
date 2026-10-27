using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;
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
        if (_demo.IsProtected(username))
            throw new BusinessRuleException("La contraseña de esta cuenta no se puede cambiar desde el sistema");

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

    public async Task<int> GetLinkedEmployeeIdAsync(string username)
    {
        var employeeId = await _context.Users
            .Where(u => u.Username == username)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();

        return employeeId ?? throw new BusinessRuleException("Tu cuenta no está vinculada a ningún empleado. Pide a RH que la vincule.");
    }
}

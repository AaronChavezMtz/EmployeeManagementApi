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

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _context.Users.Include(u => u.Employee).OrderBy(u => u.Username).ToListAsync();
        return users.Select(u => UserDto.From(u, _demo.IsProtected(u.Username)));
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserDto dto, string currentUsername)
    {
        var user = await _context.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            throw new NotFoundException($"No se encontró el usuario con id {id}");

        if (_demo.IsProtected(user.Username))
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

    public async Task<int> GetLinkedEmployeeIdAsync(string username)
    {
        var employeeId = await _context.Users
            .Where(u => u.Username == username)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();

        return employeeId ?? throw new BusinessRuleException("Tu cuenta no está vinculada a ningún empleado. Pide a RH que la vincule.");
    }
}

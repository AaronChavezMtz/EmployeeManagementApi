using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        return await _context.Users
            .OrderBy(u => u.Username)
            .Select(u => new UserDto
            {
                Id = u.Id, Username = u.Username, Email = u.Email,
                Role = u.Role.ToString(), IsActive = u.IsActive, CreatedAt = u.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserDto dto, string currentUsername)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is null)
            throw new NotFoundException($"No se encontró el usuario con id {id}");

        if (!Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var role))
            throw new BusinessRuleException("El rol debe ser 'Admin' o 'Viewer'");

        // Evita que un admin se quite su propio rol o se desactive a sí mismo por accidente,
        // lo cual podría dejar al sistema sin ningún administrador activo.
        if (user.Username == currentUsername && (role != UserRole.Admin || !dto.IsActive))
            throw new BusinessRuleException("No puedes quitarte tu propio rol de administrador ni desactivar tu propia cuenta");

        user.Role = role;
        user.IsActive = dto.IsActive;
        await _context.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id, Username = user.Username, Email = user.Email,
            Role = user.Role.ToString(), IsActive = user.IsActive, CreatedAt = user.CreatedAt
        };
    }
}

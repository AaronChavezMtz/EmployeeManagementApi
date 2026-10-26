using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Services;

// Reglas del vínculo usuario ↔ empleado, compartidas por el alta (AuthService) y la edición (UserService).
public static class UserLinkRules
{
    // Devuelve el EmployeeId a guardar: obligatorio y válido para Employee, siempre null para Admin.
    public static async Task<int?> ResolveEmployeeIdAsync(AppDbContext context, UserRole role, int? employeeId, int? ignoreUserId = null)
    {
        if (role == UserRole.Admin)
            return null;

        if (employeeId is null)
            throw new BusinessRuleException("Un usuario con rol Employee debe estar vinculado a un empleado");

        var employee = await context.Employees.FindAsync(employeeId.Value)
            ?? throw new BusinessRuleException($"El empleado con id {employeeId} no existe");
        if (!employee.IsActive)
            throw new BusinessRuleException("No se puede vincular un usuario a un empleado dado de baja");

        var alreadyLinked = await context.Users.AnyAsync(u => u.EmployeeId == employeeId && u.Id != ignoreUserId);
        if (alreadyLinked)
            throw new BusinessRuleException("Ese empleado ya tiene un usuario vinculado");

        return employeeId;
    }
}

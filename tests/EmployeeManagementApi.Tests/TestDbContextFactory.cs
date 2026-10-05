using EmployeeManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Tests;

// Crea un AppDbContext en memoria, aislado por prueba (base nueva por Guid).
// Se usa el proveedor InMemory de EF Core (no SQL Server real) para que las
// pruebas corran rápido y sin depender de infraestructura externa.
// Los 4 departamentos semilla (HasData en AppDbContext) se aplican solos
// al llamar EnsureCreated().
public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

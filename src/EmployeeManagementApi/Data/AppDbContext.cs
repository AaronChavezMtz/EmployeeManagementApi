using EmployeeManagementApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<EmployeeHistory> EmployeeHistories => Set<EmployeeHistory>();

    // Entidad sin clave, mapeada a la vista vw_DepartmentSummary (ver Database/schema.sql).
    public DbSet<DepartmentSummaryView> DepartmentSummaries => Set<DepartmentSummaryView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(d => d.Name).IsUnique();
            entity.Property(d => d.Description).HasMaxLength(300);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(80);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(80);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Position).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Salary).HasColumnType("decimal(12,2)");

            entity.HasOne(e => e.Department)
                  .WithMany(d => d.Employees)
                  .HasForeignKey(e => e.DepartmentId)
                  .OnDelete(DeleteBehavior.Restrict); // no permite borrar un departamento con empleados
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<EmployeeHistory>(entity =>
        {
            entity.ToTable("EmployeeHistories");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.ChangeType).HasConversion<string>().HasMaxLength(30);
            entity.Property(h => h.ChangedBy).IsRequired().HasMaxLength(50);
            entity.Property(h => h.OldValue).HasMaxLength(500);
            entity.Property(h => h.NewValue).HasMaxLength(500);

            entity.HasOne(h => h.Employee)
                  .WithMany()
                  .HasForeignKey(h => h.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(h => h.EmployeeId);
        });

        // Mapeo de la vista SQL (sin clave primaria, solo lectura).
        modelBuilder.Entity<DepartmentSummaryView>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_DepartmentSummary");
        });

        // Datos semilla: se insertan con la migración inicial.
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "Tecnología", Description = "Desarrollo de software e infraestructura", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 2, Name = "Recursos Humanos", Description = "Gestión de personal y nómina", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 3, Name = "Ventas", Description = "Comercialización y atención a clientes", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 4, Name = "Finanzas", Description = "Contabilidad y control financiero", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}

using EmployeeManagementApi.Common;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EmployeeManagementApi.Tests.Services;

public class EmployeeServiceTests
{
    private static EmployeeService CreateService(out EmployeeManagementApi.Data.AppDbContext context)
    {
        context = TestDbContextFactory.Create();
        return new EmployeeService(context, NullLogger<EmployeeService>.Instance, FakeHttpContextAccessor.Create());
    }

    private static CreateEmployeeDto ValidEmployee(string email = "juan.perez@test.com", int departmentId = 1) => new()
    {
        FirstName = "Juan",
        LastName = "Pérez",
        Email = email,
        Phone = "7731234567",
        Position = "Desarrollador",
        Salary = 18000,
        HireDate = DateTime.UtcNow.AddMonths(-1),
        BirthDate = DateTime.UtcNow.AddYears(-25),
        DepartmentId = departmentId
    };

    [Fact]
    public async Task CreateAsync_ConDatosValidos_CreaEmpleadoActivo()
    {
        var service = CreateService(out _);

        var result = await service.CreateAsync(ValidEmployee());

        result.Id.Should().BeGreaterThan(0);
        result.IsActive.Should().BeTrue();
        result.DepartmentName.Should().Be("Tecnología");
    }

    [Fact]
    public async Task CreateAsync_ConCorreoDuplicado_LanzaBusinessRuleException()
    {
        var service = CreateService(out _);
        await service.CreateAsync(ValidEmployee(email: "repetido@test.com"));

        var act = async () => await service.CreateAsync(ValidEmployee(email: "repetido@test.com"));

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*repetido@test.com*");
    }

    [Fact]
    public async Task CreateAsync_ConDepartamentoInexistente_LanzaBusinessRuleException()
    {
        var service = CreateService(out _);

        var act = async () => await service.CreateAsync(ValidEmployee(departmentId: 999));

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task GetByIdAsync_ConIdInexistente_LanzaNotFoundException()
    {
        var service = CreateService(out _);

        var act = async () => await service.GetByIdAsync(999);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_HaceBajaLogica_NoBorraElRegistro()
    {
        var service = CreateService(out var context);
        var created = await service.CreateAsync(ValidEmployee());

        await service.DeleteAsync(created.Id);

        var employeeInDb = await context.Employees.FindAsync(created.Id);
        employeeInDb.Should().NotBeNull();
        employeeInDb!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_FiltraPorDepartamentoYEstadoActivo()
    {
        var service = CreateService(out _);
        var e1 = await service.CreateAsync(ValidEmployee(email: "a@test.com", departmentId: 1));
        await service.CreateAsync(ValidEmployee(email: "b@test.com", departmentId: 2));
        await service.DeleteAsync(e1.Id); // queda inactivo

        var result = await service.GetAllAsync(new EmployeeQueryParameters
        {
            DepartmentId = 1,
            IsActive = true,
            PageNumber = 1,
            PageSize = 10
        });

        result.TotalCount.Should().Be(0); // el único de depto 1 quedó inactivo
    }

    [Fact]
    public async Task GetAllAsync_BusquedaPorTexto_EncuentraPorNombreOCorreo()
    {
        var service = CreateService(out _);
        await service.CreateAsync(ValidEmployee(email: "maria.lopez@test.com"));

        var result = await service.GetAllAsync(new EmployeeQueryParameters { Search = "maria", PageSize = 10 });

        result.TotalCount.Should().Be(1);
        result.Items.First().Email.Should().Be("maria.lopez@test.com");
    }

    [Fact]
    public async Task UpdateAsync_ConCorreoDeOtroEmpleado_LanzaBusinessRuleException()
    {
        var service = CreateService(out _);
        await service.CreateAsync(ValidEmployee(email: "uno@test.com"));
        var dos = await service.CreateAsync(ValidEmployee(email: "dos@test.com"));

        var update = new UpdateEmployeeDto
        {
            FirstName = "Dos",
            LastName = "Actualizado",
            Email = "uno@test.com", // ya usado por el primer empleado
            Position = "QA",
            Salary = 20000,
            BirthDate = DateTime.UtcNow.AddYears(-30),
            DepartmentId = 1,
            IsActive = true
        };

        var act = async () => await service.UpdateAsync(dos.Id, update);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task CreateAsync_RegistraUnaEntradaDeHistorialTipoCreated()
    {
        var service = CreateService(out _);
        var created = await service.CreateAsync(ValidEmployee());

        var history = await service.GetHistoryAsync(created.Id);

        history.Should().ContainSingle(h => h.ChangeType == EmployeeManagementApi.Models.HistoryChangeType.Created);
    }

    [Fact]
    public async Task UpdateAsync_AlCambiarDepartamento_RegistraHistorialDepartmentChanged()
    {
        var service = CreateService(out _);
        var created = await service.CreateAsync(ValidEmployee(departmentId: 1));

        var update = new UpdateEmployeeDto
        {
            FirstName = created.FirstName, LastName = created.LastName, Email = created.Email,
            Position = created.Position, Salary = created.Salary, BirthDate = created.BirthDate,
            DepartmentId = 2, IsActive = true
        };
        await service.UpdateAsync(created.Id, update);

        var history = await service.GetHistoryAsync(created.Id);

        history.Should().Contain(h => h.ChangeType == EmployeeManagementApi.Models.HistoryChangeType.DepartmentChanged);
    }
}

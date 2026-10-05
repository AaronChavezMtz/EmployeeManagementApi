using EmployeeManagementApi.Common;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EmployeeManagementApi.Tests.Services;

public class DepartmentServiceTests
{
    private static (DepartmentService deptService, EmployeeService empService) CreateServices()
    {
        var context = TestDbContextFactory.Create();
        return (new DepartmentService(context), new EmployeeService(context, NullLogger<EmployeeService>.Instance, FakeHttpContextAccessor.Create()));
    }

    [Fact]
    public async Task CreateAsync_ConNombreDuplicado_LanzaBusinessRuleException()
    {
        var (deptService, _) = CreateServices();

        var act = async () => await deptService.CreateAsync(new CreateDepartmentDto { Name = "Tecnología" });

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task DeleteAsync_ConEmpleadosAsignados_LanzaBusinessRuleException()
    {
        var (deptService, empService) = CreateServices();
        await empService.CreateAsync(new CreateEmployeeDto
        {
            FirstName = "Ana",
            LastName = "Gómez",
            Email = "ana@test.com",
            Position = "Analista",
            Salary = 15000,
            HireDate = DateTime.UtcNow,
            BirthDate = DateTime.UtcNow.AddYears(-22),
            DepartmentId = 1
        });

        var act = async () => await deptService.DeleteAsync(1);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*empleados asignados*");
    }

    [Fact]
    public async Task DeleteAsync_SinEmpleados_EliminaCorrectamente()
    {
        var (deptService, _) = CreateServices();

        await deptService.DeleteAsync(2); // "Recursos Humanos", sin empleados

        var act = async () => await deptService.GetByIdAsync(2);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAllAsync_CuentaSoloEmpleadosActivos()
    {
        var (deptService, empService) = CreateServices();
        var dto = new CreateEmployeeDto
        {
            FirstName = "Luis", LastName = "Ruiz", Email = "luis@test.com", Position = "Dev",
            Salary = 16000, HireDate = DateTime.UtcNow, BirthDate = DateTime.UtcNow.AddYears(-20), DepartmentId = 1
        };
        var created = await empService.CreateAsync(dto);
        await empService.DeleteAsync(created.Id); // inactivo

        var departments = await deptService.GetAllAsync();

        departments.First(d => d.Id == 1).EmployeeCount.Should().Be(0);
    }
}

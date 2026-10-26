using EmployeeManagementApi.Common;
using EmployeeManagementApi.Data;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Models;
using EmployeeManagementApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EmployeeManagementApi.Tests.Services;

public class LeaveServiceTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    // Próximo lunes a partir de dentro de una semana: un rango de lunes a viernes siempre en el futuro.
    private static readonly DateTime Monday = Today.AddDays(((int)DayOfWeek.Monday - (int)Today.DayOfWeek + 7) % 7 + 7);

    private static (LeaveService Service, AppDbContext Context, Employee Employee) Create(int hiredYearsAgo = 3)
    {
        var context = TestDbContextFactory.Create();
        var employee = new Employee
        {
            FirstName = "Ana", LastName = "Garcia", Email = "ana@test.com", Position = "Dev", Salary = 1, DepartmentId = 1,
            BirthDate = Today.AddYears(-30), HireDate = Today.AddYears(-hiredYearsAgo).AddDays(-10)
        };
        context.Employees.Add(employee);
        context.SaveChanges();
        return (new LeaveService(context, NullLogger<LeaveService>.Instance), context, employee);
    }

    private static CreateLeaveRequestDto Vacation(DateTime start, DateTime end) =>
        new() { Type = LeaveType.Vacation, StartDate = start, EndDate = end };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 12)]
    [InlineData(2, 14)]
    [InlineData(5, 20)]
    [InlineData(6, 22)]
    [InlineData(10, 22)]
    [InlineData(11, 24)]
    [InlineData(16, 26)]
    public void VacationDaysFor_SigueLaTablaDeLaLft(int years, int expected)
        => LeaveService.VacationDaysFor(years).Should().Be(expected);

    [Fact]
    public void BusinessDays_NoCuentaFinesDeSemana()
        => LeaveService.BusinessDays(Monday, Monday.AddDays(6)).Should().Be(5); // lunes a domingo

    [Fact]
    public async Task CreateAsync_Vacaciones_DescuentaDelSaldo()
    {
        var (service, _, employee) = Create(hiredYearsAgo: 3); // 16 días

        var request = await service.CreateAsync(employee.Id, Vacation(Monday, Monday.AddDays(4)));
        var balance = await service.GetBalanceAsync(employee.Id);

        request.Days.Should().Be(5);
        request.Status.Should().Be(LeaveStatus.Pending);
        balance.EntitledDays.Should().Be(16);
        balance.PendingDays.Should().Be(5);
        balance.AvailableDays.Should().Be(11);
    }

    [Fact]
    public async Task CreateAsync_SinPrimerAnioCumplido_LanzaBusinessRuleException()
    {
        var (service, _, employee) = Create(hiredYearsAgo: 0);

        var act = async () => await service.CreateAsync(employee.Id, Vacation(Monday, Monday));

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task CreateAsync_QueExcedeElSaldo_LanzaBusinessRuleException()
    {
        var (service, _, employee) = Create(hiredYearsAgo: 1); // 12 días

        var act = async () => await service.CreateAsync(employee.Id, Vacation(Monday, Monday.AddDays(17))); // 14 hábiles

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task CreateAsync_QueSeSolapaConOtraPendiente_LanzaBusinessRuleException()
    {
        var (service, _, employee) = Create();
        await service.CreateAsync(employee.Id, Vacation(Monday, Monday.AddDays(2)));

        var act = async () => await service.CreateAsync(employee.Id,
            new CreateLeaveRequestDto { Type = LeaveType.Personal, StartDate = Monday.AddDays(1), EndDate = Monday.AddDays(1) });

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task CreateAsync_VacacionesEnElPasado_LanzaBusinessRuleException()
    {
        var (service, _, employee) = Create();

        var act = async () => await service.CreateAsync(employee.Id, Vacation(Today.AddDays(-3), Today.AddDays(-1)));

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task CreateAsync_IncapacidadRecientePasada_SePermite()
    {
        var (service, _, employee) = Create();
        var start = Today.AddDays(-5);

        var request = await service.CreateAsync(employee.Id,
            new CreateLeaveRequestDto { Type = LeaveType.Sick, StartDate = start, EndDate = start.AddDays(1) });

        request.Type.Should().Be(LeaveType.Sick);
    }

    [Fact]
    public async Task RejectAsync_SinComentario_LanzaBusinessRuleException()
    {
        var (service, _, employee) = Create();
        var request = await service.CreateAsync(employee.Id, Vacation(Monday, Monday));

        var act = async () => await service.RejectAsync(request.Id, "rh", "  ");

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task ApproveAsync_GuardaRevisorYYaNoSePuedeVolverARevisar()
    {
        var (service, _, employee) = Create();
        var request = await service.CreateAsync(employee.Id, Vacation(Monday, Monday.AddDays(1)));

        var approved = await service.ApproveAsync(request.Id, "rh", "Disfrútalas");
        var act = async () => await service.RejectAsync(request.Id, "rh", "Cambio de opinión");

        approved.Status.Should().Be(LeaveStatus.Approved);
        approved.ReviewedBy.Should().Be("rh");
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task CancelAsync_DeOtroEmpleado_LanzaNotFoundException()
    {
        var (service, context, employee) = Create();
        var request = await service.CreateAsync(employee.Id, Vacation(Monday, Monday));
        var otro = new Employee
        {
            FirstName = "Luis", LastName = "R", Email = "luis@test.com", Position = "Dev", Salary = 1, DepartmentId = 1,
            BirthDate = Today.AddYears(-30), HireDate = Today.AddYears(-2)
        };
        context.Employees.Add(otro);
        await context.SaveChangesAsync();

        var act = async () => await service.CancelAsync(otro.Id, request.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

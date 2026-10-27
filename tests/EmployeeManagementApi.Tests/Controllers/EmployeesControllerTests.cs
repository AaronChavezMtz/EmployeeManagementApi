using EmployeeManagementApi.Common;
using EmployeeManagementApi.Controllers;
using EmployeeManagementApi.DTOs;
using EmployeeManagementApi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EmployeeManagementApi.Tests.Controllers;

public class EmployeesControllerTests
{
    private readonly Mock<IEmployeeService> _serviceMock = new();
    private readonly Mock<IUserService> _usersMock = new();
    private readonly EmployeesController _controller;

    public EmployeesControllerTests()
    {
        _controller = new EmployeesController(_serviceMock.Object, _usersMock.Object);
    }

    [Fact]
    public async Task GetById_CuandoExiste_DevuelveOkConElEmpleado()
    {
        var dto = new EmployeeDto { Id = 5, FirstName = "Luis", LastName = "Soto" };
        _serviceMock.Setup(s => s.GetByIdAsync(5)).ReturnsAsync(dto);

        var result = await _controller.GetById(5);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task Create_DelegaEnElServicioYDevuelveCreatedAtAction()
    {
        var createDto = new CreateEmployeeDto { FirstName = "Eva", LastName = "Díaz", DepartmentId = 1 };
        var created = new EmployeeDto { Id = 10, FirstName = "Eva", LastName = "Díaz" };
        _serviceMock.Setup(s => s.CreateAsync(createDto)).ReturnsAsync(created);

        var result = await _controller.Create(createDto);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().BeEquivalentTo(created);
        _serviceMock.Verify(s => s.CreateAsync(createDto), Times.Once);
    }

    [Fact]
    public async Task Terminate_DelegaEnElServicioYDevuelveOk()
    {
        var dto = new TerminateEmployeeDto { TerminationDate = DateTime.UtcNow, Reason = EmployeeManagementApi.Models.TerminationReason.Resignation };
        _serviceMock.Setup(s => s.TerminateAsync(7, dto)).ReturnsAsync(new EmployeeDto { Id = 7, IsActive = false });

        var result = await _controller.Terminate(7, dto);

        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.TerminateAsync(7, dto), Times.Once);
    }

    [Fact]
    public async Task GetById_CuandoNoExiste_PropagaNotFoundException()
    {
        _serviceMock.Setup(s => s.GetByIdAsync(99)).ThrowsAsync(new NotFoundException("No existe"));

        var act = async () => await _controller.GetById(99);

        // El controller no atrapa la excepción: eso lo hace ExceptionMiddleware a nivel de pipeline.
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_ConAccesoAlPortal_ValidaAntesDeCrearYDevuelveCredenciales()
    {
        var dto = new CreateEmployeeDto { FirstName = "Eva", LastName = "Díaz", Email = "eva@test.com", DepartmentId = 1, GrantPortalAccess = true };
        _serviceMock.Setup(s => s.CreateAsync(dto)).ReturnsAsync(new EmployeeDto { Id = 5 });
        _usersMock.Setup(u => u.CreatePortalAccessAsync(5)).ReturnsAsync(new PortalAccessDto { Username = "eva@test.com", TemporaryPassword = "Temp12345678" });

        var result = await _controller.Create(dto);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.Value.As<EmployeeDto>().PortalAccess!.TemporaryPassword.Should().Be("Temp12345678");
        _usersMock.Verify(u => u.EnsurePortalUsernameAvailableAsync("eva@test.com"), Times.Once);
    }

    [Fact]
    public async Task Create_SiElUsuarioYaExiste_NoCreaAlEmpleado()
    {
        var dto = new CreateEmployeeDto { Email = "eva@test.com", DepartmentId = 1, GrantPortalAccess = true };
        _usersMock.Setup(u => u.EnsurePortalUsernameAvailableAsync("eva@test.com")).ThrowsAsync(new BusinessRuleException("Ya existe"));

        var act = async () => await _controller.Create(dto);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _serviceMock.Verify(s => s.CreateAsync(It.IsAny<CreateEmployeeDto>()), Times.Never);
    }
}

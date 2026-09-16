using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.Employees;

namespace WorkLogManager.Application.Tests.UseCases.Employees;

public class GetEmployeeByIdUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_EmployeeExists_ReturnsEmployee()
    {
        var employee = EntityFactory.CreateEmployee();

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var useCase = new GetEmployeeByIdUseCase(employeeRepository.Object);

        var result = await useCase.ExecuteAsync(employee.Id);

        Assert.Equal(employee.Id, result.Id);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeDoesNotExist_ThrowsNotFoundException()
    {
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var useCase = new GetEmployeeByIdUseCase(employeeRepository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }
}

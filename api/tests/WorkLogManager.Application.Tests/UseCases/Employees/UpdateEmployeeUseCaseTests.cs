using FluentValidation;
using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.Employees;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.Employees;

public class UpdateEmployeeUseCaseTests
{
    private static UpdateEmployeeUseCase CreateUseCase(Mock<IEmployeeRepository> repository)
    {
        return new UpdateEmployeeUseCase(repository.Object, new EmployeeValidator());
    }

    [Fact]
    public async Task ExecuteAsync_ValidData_UpdatesEmployeeFieldsAndTouchesTimestamp()
    {
        var existingEmployee = EntityFactory.CreateEmployee(name: "Jane Doe", role: "Developer");
        var originalUpdatedAt = existingEmployee.UpdatedAtUtc;

        var repository = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetByIdAsync(existingEmployee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingEmployee);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateEmployee(name: "Jane Smith", role: "Senior Developer", hireDate: new DateOnly(2021, 1, 1));

        var result = await useCase.ExecuteAsync(existingEmployee.Id, input);

        Assert.Same(existingEmployee, result);
        Assert.Equal("Jane Smith", result.Name);
        Assert.Equal("Senior Developer", result.Role);
        Assert.True(result.UpdatedAtUtc >= originalUpdatedAt);
        repository.Verify(r => r.UpdateAsync(existingEmployee, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistingEmployee_ThrowsNotFoundException()
    {
        var repository = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateEmployee();

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), input));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRole_ThrowsValidationException()
    {
        var existingEmployee = EntityFactory.CreateEmployee();

        var repository = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetByIdAsync(existingEmployee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingEmployee);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateEmployee(role: string.Empty);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(existingEmployee.Id, input));

        repository.Verify(r => r.UpdateAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

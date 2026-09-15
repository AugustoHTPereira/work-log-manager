using FluentValidation;
using Moq;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.Employees;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.Employees;

public class CreateEmployeeUseCaseTests
{
    private static CreateEmployeeUseCase CreateUseCase(Mock<IEmployeeRepository> repository, IValidator<Employee>? validator = null)
    {
        return new CreateEmployeeUseCase(repository.Object, validator ?? new EmployeeValidator());
    }

    [Fact]
    public async Task ExecuteAsync_ValidData_AddsEmployeeWithNewIdAndTimestamps()
    {
        var repository = new Mock<IEmployeeRepository>();
        var useCase = CreateUseCase(repository);
        var employee = EntityFactory.CreateEmployee(name: "Jane Doe", id: Guid.Empty);

        var result = await useCase.ExecuteAsync(employee);

        Assert.Equal("Jane Doe", result.Name);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.True(result.CreatedAtUtc > DateTimeOffset.MinValue);
        Assert.Equal(result.CreatedAtUtc, result.UpdatedAtUtc);
        repository.Verify(r => r.AddAsync(employee, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyName_ThrowsValidationExceptionAndDoesNotCallRepository()
    {
        var repository = new Mock<IEmployeeRepository>();
        var useCase = CreateUseCase(repository);
        var employee = EntityFactory.CreateEmployee(name: string.Empty);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(employee));

        repository.Verify(r => r.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NonPositiveDailyWorkHours_ThrowsValidationException()
    {
        var repository = new Mock<IEmployeeRepository>();
        var useCase = CreateUseCase(repository);
        var employee = EntityFactory.CreateEmployee(dailyWorkHours: 0m);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(employee));
    }
}

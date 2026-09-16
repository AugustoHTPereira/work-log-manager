using FluentValidation;
using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.WorkSchedules;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.WorkSchedules;

public class UpdateEmployeeWorkScheduleUseCaseTests
{
    private static (UpdateEmployeeWorkScheduleUseCase UseCase, Mock<IEmployeeRepository> EmployeeRepository, Mock<IWorkSchedulePeriodRepository> WorkSchedulePeriodRepository) CreateSut()
    {
        var employeeRepository = new Mock<IEmployeeRepository>();
        var workSchedulePeriodRepository = new Mock<IWorkSchedulePeriodRepository>();
        var useCase = new UpdateEmployeeWorkScheduleUseCase(employeeRepository.Object, workSchedulePeriodRepository.Object, new WorkSchedulePeriodValidator());
        return (useCase, employeeRepository, workSchedulePeriodRepository);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeExists_ReplacesEmployeeScheduleAndAssignsEmployeeId()
    {
        var (useCase, employeeRepository, workSchedulePeriodRepository) = CreateSut();
        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var input = new List<WorkSchedulePeriod> { EntityFactory.CreateWorkSchedulePeriod() };

        var result = await useCase.ExecuteAsync(employee.Id, input);

        Assert.Single(result);
        Assert.Equal(employee.Id, result[0].EmployeeId);
        workSchedulePeriodRepository.Verify(r => r.ReplaceAsync(employee.Id, input, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleNonOverlappingPeriodsSameDay_BothArePersisted()
    {
        var (useCase, employeeRepository, workSchedulePeriodRepository) = CreateSut();
        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var input = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(13, 0), endTime: new TimeOnly(17, 0)),
        };

        var result = await useCase.ExecuteAsync(employee.Id, input);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeDoesNotExist_ThrowsNotFoundException()
    {
        var (useCase, employeeRepository, _) = CreateSut();
        employeeRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), []));
    }

    [Fact]
    public async Task ExecuteAsync_OverlappingPeriodsSameDay_ThrowsDomainException()
    {
        var (useCase, employeeRepository, workSchedulePeriodRepository) = CreateSut();
        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var input = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(11, 0), endTime: new TimeOnly(15, 0)),
        };

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(employee.Id, input));

        workSchedulePeriodRepository.Verify(r => r.ReplaceAsync(It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<WorkSchedulePeriod>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

using FluentValidation;
using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.WorkSchedules;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.WorkSchedules;

public class UpdateGeneralWorkScheduleUseCaseTests
{
    private static UpdateGeneralWorkScheduleUseCase CreateUseCase(Mock<IWorkSchedulePeriodRepository> repository)
    {
        return new UpdateGeneralWorkScheduleUseCase(repository.Object, new WorkSchedulePeriodValidator());
    }

    [Fact]
    public async Task ExecuteAsync_ValidSinglePeriod_ReplacesGeneralScheduleAndAssignsNewIdAndTimestamps()
    {
        var repository = new Mock<IWorkSchedulePeriodRepository>();
        var useCase = CreateUseCase(repository);

        var period = EntityFactory.CreateWorkSchedulePeriod(employeeId: Guid.NewGuid(), id: Guid.Empty);
        var input = new List<WorkSchedulePeriod> { period };

        var result = await useCase.ExecuteAsync(input);

        Assert.Single(result);
        Assert.Null(result[0].EmployeeId);
        Assert.NotEqual(Guid.Empty, result[0].Id);
        repository.Verify(r => r.ReplaceAsync(null, input, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TwoNonOverlappingPeriodsSameDay_BothArePersisted()
    {
        var repository = new Mock<IWorkSchedulePeriodRepository>();
        var useCase = CreateUseCase(repository);

        var input = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(13, 0), endTime: new TimeOnly(17, 0)),
        };

        var result = await useCase.ExecuteAsync(input);

        Assert.Equal(2, result.Count);
        repository.Verify(r => r.ReplaceAsync(null, It.Is<IReadOnlyList<WorkSchedulePeriod>>(p => p.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OverlappingPeriodsSameDay_ThrowsDomainExceptionAndDoesNotPersist()
    {
        var repository = new Mock<IWorkSchedulePeriodRepository>();
        var useCase = CreateUseCase(repository);

        var input = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(11, 0), endTime: new TimeOnly(15, 0)),
        };

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(input));

        repository.Verify(r => r.ReplaceAsync(It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<WorkSchedulePeriod>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EndTimeNotAfterStartTime_ThrowsValidationException()
    {
        var repository = new Mock<IWorkSchedulePeriodRepository>();
        var useCase = CreateUseCase(repository);

        var input = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(startTime: new TimeOnly(12, 0), endTime: new TimeOnly(8, 0)),
        };

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(input));

        repository.Verify(r => r.ReplaceAsync(It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<WorkSchedulePeriod>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

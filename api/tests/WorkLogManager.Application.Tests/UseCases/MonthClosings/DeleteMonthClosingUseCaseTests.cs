using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.MonthClosings;

namespace WorkLogManager.Application.Tests.UseCases.MonthClosings;

public class DeleteMonthClosingUseCaseTests
{
    private static (
        Mock<IMonthClosingRepository> MonthClosingRepository,
        Mock<IEmployeeWorkLogRepository> EmployeeWorkLogRepository,
        DeleteMonthClosingUseCase UseCase) BuildUseCase()
    {
        var monthClosingRepository = new Mock<IMonthClosingRepository>();
        var employeeWorkLogRepository = new Mock<IEmployeeWorkLogRepository>();

        var useCase = new DeleteMonthClosingUseCase(monthClosingRepository.Object, employeeWorkLogRepository.Object);

        return (monthClosingRepository, employeeWorkLogRepository, useCase);
    }

    [Fact]
    public async Task ExecuteAsync_MonthClosingDoesNotExist_ThrowsNotFoundException()
    {
        var (monthClosingRepository, employeeWorkLogRepository, useCase) = BuildUseCase();
        monthClosingRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MonthClosing?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));

        employeeWorkLogRepository.Verify(r => r.ListByMonthClosingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        employeeWorkLogRepository.Verify(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()), Times.Never);
        employeeWorkLogRepository.Verify(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()), Times.Never);
        monthClosingRepository.Verify(r => r.DeleteAsync(It.IsAny<MonthClosing>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_MonthClosingExists_DeletesTheMonthClosing()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var (monthClosingRepository, employeeWorkLogRepository, useCase) = BuildUseCase();

        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await useCase.ExecuteAsync(monthClosing.Id);

        monthClosingRepository.Verify(r => r.DeleteAsync(monthClosing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AutomaticWorkLogsLinked_AreAllPassedToDeleteRange()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var automaticOne = EntityFactory.CreateEmployeeWorkLog(origin: WorkLogOrigin.Automatic);
        var automaticTwo = EntityFactory.CreateEmployeeWorkLog(origin: WorkLogOrigin.Automatic);

        var (monthClosingRepository, employeeWorkLogRepository, useCase) = BuildUseCase();
        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([automaticOne, automaticTwo]);

        IEnumerable<EmployeeWorkLog>? deletedWorkLogs = null;
        employeeWorkLogRepository
            .Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((workLogs, _) => deletedWorkLogs = workLogs)
            .Returns(Task.CompletedTask);

        await useCase.ExecuteAsync(monthClosing.Id);

        Assert.NotNull(deletedWorkLogs);
        Assert.Equal([automaticOne, automaticTwo], deletedWorkLogs);
    }

    [Fact]
    public async Task ExecuteAsync_ManualWorkLogsLinked_AreUnlinkedAndTouchedInsteadOfDeleted()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var manualWorkLog = EntityFactory.CreateEmployeeWorkLog(origin: WorkLogOrigin.Manual);
        manualWorkLog.MonthClosingId = monthClosing.Id;
        var originalUpdatedAt = manualWorkLog.UpdatedAtUtc;

        var (monthClosingRepository, employeeWorkLogRepository, useCase) = BuildUseCase();
        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([manualWorkLog]);

        IEnumerable<EmployeeWorkLog>? updatedWorkLogs = null;
        employeeWorkLogRepository
            .Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((workLogs, _) => updatedWorkLogs = workLogs)
            .Returns(Task.CompletedTask);

        IEnumerable<EmployeeWorkLog>? deletedWorkLogs = null;
        employeeWorkLogRepository
            .Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((workLogs, _) => deletedWorkLogs = workLogs)
            .Returns(Task.CompletedTask);

        await useCase.ExecuteAsync(monthClosing.Id);

        Assert.NotNull(updatedWorkLogs);
        var updatedList = updatedWorkLogs!.ToList();
        Assert.Single(updatedList);
        Assert.Null(updatedList[0].MonthClosingId);
        Assert.True(updatedList[0].UpdatedAtUtc > originalUpdatedAt);

        Assert.NotNull(deletedWorkLogs);
        Assert.Empty(deletedWorkLogs!);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotConsultSystemParameterRepository()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var manualWorkLog = EntityFactory.CreateEmployeeWorkLog(origin: WorkLogOrigin.Manual);

        var (monthClosingRepository, employeeWorkLogRepository, useCase) = BuildUseCase();
        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([manualWorkLog]);

        var systemParameterRepository = new Mock<ISystemParameterRepository>(MockBehavior.Strict);

        await useCase.ExecuteAsync(monthClosing.Id);

        systemParameterRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_OldMonthClosing_IsDeletedWithoutAnyAgeRestriction()
    {
        var oldClosing = EntityFactory.CreateMonthClosing(month: 1, year: 2015);
        var (monthClosingRepository, employeeWorkLogRepository, useCase) = BuildUseCase();

        monthClosingRepository.Setup(r => r.GetByIdAsync(oldClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(oldClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await useCase.ExecuteAsync(oldClosing.Id);

        monthClosingRepository.Verify(r => r.DeleteAsync(oldClosing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_MixedOriginWorkLogs_ResultContainsCorrectCounts()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var automaticOne = EntityFactory.CreateEmployeeWorkLog(origin: WorkLogOrigin.Automatic);
        var automaticTwo = EntityFactory.CreateEmployeeWorkLog(origin: WorkLogOrigin.Automatic);
        var manualOne = EntityFactory.CreateEmployeeWorkLog(origin: WorkLogOrigin.Manual);

        var (monthClosingRepository, employeeWorkLogRepository, useCase) = BuildUseCase();
        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([automaticOne, automaticTwo, manualOne]);

        var result = await useCase.ExecuteAsync(monthClosing.Id);

        Assert.Equal(monthClosing.Id, result.MonthClosingId);
        Assert.Equal(monthClosing.Month, result.Month);
        Assert.Equal(monthClosing.Year, result.Year);
        Assert.Equal(2, result.DeletedAutomaticWorkLogsCount);
        Assert.Equal(1, result.UnlinkedManualWorkLogsCount);
    }
}

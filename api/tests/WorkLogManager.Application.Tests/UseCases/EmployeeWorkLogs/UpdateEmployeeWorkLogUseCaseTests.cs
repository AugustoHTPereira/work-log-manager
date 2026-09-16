using FluentValidation;
using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.EmployeeWorkLogs;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.EmployeeWorkLogs;

public class UpdateEmployeeWorkLogUseCaseTests
{
    private static Mock<IMonthClosingRepository> CreateOpenMonthClosingRepository()
    {
        var repository = new Mock<IMonthClosingRepository>();
        repository
            .Setup(r => r.GetByMonthYearAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MonthClosing?)null);
        return repository;
    }

    private static Mock<ISystemParameterRepository> CreateNotConfiguredSystemParameterRepository()
    {
        var repository = new Mock<ISystemParameterRepository>();
        repository
            .Setup(r => r.ListByParamAsync(It.IsAny<SystemParameterName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[]);
        return repository;
    }

    private static Mock<ISystemParameterRepository> CreateAllowManageClosedWorkLogsRepository(bool allow)
    {
        var repository = new Mock<ISystemParameterRepository>();
        repository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AllowManageClosedWorkLogs, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[
                new SystemParameter
                {
                    Id = Guid.NewGuid(),
                    Param = SystemParameterName.AllowManageClosedWorkLogs,
                    Value = allow ? "true" : "false",
                    ValueType = SystemParameterValueType.Bool,
                },
            ]);
        return repository;
    }

    private static UpdateEmployeeWorkLogUseCase CreateUseCase(
        Mock<IEmployeeWorkLogRepository> repository,
        Mock<IMonthClosingRepository>? monthClosingRepository = null,
        Mock<ISystemParameterRepository>? systemParameterRepository = null)
    {
        return new UpdateEmployeeWorkLogUseCase(
            repository.Object,
            (monthClosingRepository ?? CreateOpenMonthClosingRepository()).Object,
            (systemParameterRepository ?? CreateNotConfiguredSystemParameterRepository()).Object,
            new EmployeeWorkLogValidator());
    }

    [Fact]
    public async Task ExecuteAsync_ValidData_UpdatesWorkLogAndRecalculatesDuration()
    {
        var employeeId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var existingWorkLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId, startDate: start, endDate: start.AddHours(1));

        var repository = new Mock<IEmployeeWorkLogRepository>();
        repository.Setup(r => r.GetByIdAsync(existingWorkLog.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingWorkLog);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateEmployeeWorkLog(type: WorkLogType.Absence, startDate: start, endDate: start.AddHours(3));

        var result = await useCase.ExecuteAsync(employeeId, existingWorkLog.Id, input);

        Assert.Same(existingWorkLog, result);
        Assert.Equal(WorkLogType.Absence, result.Type);
        Assert.Equal(10800, result.DurationSeconds);
        repository.Verify(r => r.UpdateAsync(existingWorkLog, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ValidData_UpdatesNote()
    {
        var employeeId = Guid.NewGuid();
        var existingWorkLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId, note: "Original note");

        var repository = new Mock<IEmployeeWorkLogRepository>();
        repository.Setup(r => r.GetByIdAsync(existingWorkLog.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingWorkLog);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateEmployeeWorkLog(note: "Updated note");

        var result = await useCase.ExecuteAsync(employeeId, existingWorkLog.Id, input);

        Assert.Equal("Updated note", result.Note);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistingWorkLog_ThrowsNotFoundException()
    {
        var repository = new Mock<IEmployeeWorkLogRepository>();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((EmployeeWorkLog?)null);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateEmployeeWorkLog();

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), input));
    }

    [Fact]
    public async Task ExecuteAsync_WorkLogBelongsToDifferentEmployee_ThrowsNotFoundException()
    {
        var existingWorkLog = EntityFactory.CreateEmployeeWorkLog(employeeId: Guid.NewGuid());

        var repository = new Mock<IEmployeeWorkLogRepository>();
        repository.Setup(r => r.GetByIdAsync(existingWorkLog.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingWorkLog);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateEmployeeWorkLog();

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), existingWorkLog.Id, input));
    }

    [Fact]
    public async Task ExecuteAsync_EndDateBeforeStartDate_ThrowsValidationExceptionAndDoesNotCallRepository()
    {
        var employeeId = Guid.NewGuid();
        var existingWorkLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId);

        var repository = new Mock<IEmployeeWorkLogRepository>();
        repository.Setup(r => r.GetByIdAsync(existingWorkLog.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingWorkLog);

        var useCase = CreateUseCase(repository);
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var input = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start.AddHours(1));
        input.EndDate = start.AddHours(-1);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(employeeId, existingWorkLog.Id, input));

        repository.Verify(r => r.UpdateAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingWorkLogAlreadyLinkedToMonthClosing_ThrowsDomainExceptionAndDoesNotCallRepository()
    {
        var employeeId = Guid.NewGuid();
        var existingWorkLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId);
        existingWorkLog.MonthClosingId = Guid.NewGuid();

        var repository = new Mock<IEmployeeWorkLogRepository>();
        repository.Setup(r => r.GetByIdAsync(existingWorkLog.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingWorkLog);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateEmployeeWorkLog();

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(employeeId, existingWorkLog.Id, input));

        repository.Verify(r => r.UpdateAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NewStartDateFallsInAlreadyClosedMonth_ThrowsDomainExceptionAndDoesNotCallRepository()
    {
        var employeeId = Guid.NewGuid();
        var existingWorkLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId);

        var repository = new Mock<IEmployeeWorkLogRepository>();
        repository.Setup(r => r.GetByIdAsync(existingWorkLog.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingWorkLog);

        var monthClosingRepository = new Mock<IMonthClosingRepository>();
        monthClosingRepository
            .Setup(r => r.GetByMonthYearAsync(1, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntityFactory.CreateMonthClosing(month: 1, year: 2026));

        var useCase = CreateUseCase(repository, monthClosingRepository);

        var newStart = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        var input = EntityFactory.CreateEmployeeWorkLog(startDate: newStart, endDate: newStart.AddHours(1));

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(employeeId, existingWorkLog.Id, input));

        repository.Verify(r => r.UpdateAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingWorkLogLinkedToMonthClosingWithAllowManageClosedWorkLogsTrue_UpdatesNormally()
    {
        var employeeId = Guid.NewGuid();
        var existingWorkLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId, origin: WorkLogOrigin.Automatic);
        existingWorkLog.MonthClosingId = Guid.NewGuid();

        var repository = new Mock<IEmployeeWorkLogRepository>();
        repository.Setup(r => r.GetByIdAsync(existingWorkLog.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingWorkLog);

        var systemParameterRepository = CreateAllowManageClosedWorkLogsRepository(allow: true);
        var useCase = CreateUseCase(repository, systemParameterRepository: systemParameterRepository);
        var input = EntityFactory.CreateEmployeeWorkLog(type: WorkLogType.Absence);

        var result = await useCase.ExecuteAsync(employeeId, existingWorkLog.Id, input);

        Assert.Equal(WorkLogType.Absence, result.Type);
        repository.Verify(r => r.UpdateAsync(existingWorkLog, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NewStartDateInClosedMonthWithAllowManageClosedWorkLogsTrue_UpdatesNormally()
    {
        var employeeId = Guid.NewGuid();
        var existingWorkLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId);

        var repository = new Mock<IEmployeeWorkLogRepository>();
        repository.Setup(r => r.GetByIdAsync(existingWorkLog.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingWorkLog);

        var monthClosingRepository = new Mock<IMonthClosingRepository>();
        monthClosingRepository
            .Setup(r => r.GetByMonthYearAsync(1, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntityFactory.CreateMonthClosing(month: 1, year: 2026));

        var systemParameterRepository = CreateAllowManageClosedWorkLogsRepository(allow: true);
        var useCase = CreateUseCase(repository, monthClosingRepository, systemParameterRepository);

        var newStart = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        var input = EntityFactory.CreateEmployeeWorkLog(startDate: newStart, endDate: newStart.AddHours(1));

        var result = await useCase.ExecuteAsync(employeeId, existingWorkLog.Id, input);

        Assert.NotNull(result);
        repository.Verify(r => r.UpdateAsync(existingWorkLog, It.IsAny<CancellationToken>()), Times.Once);
    }
}

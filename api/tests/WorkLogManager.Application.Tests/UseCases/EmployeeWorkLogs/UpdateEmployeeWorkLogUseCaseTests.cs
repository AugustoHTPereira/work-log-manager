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
    private static UpdateEmployeeWorkLogUseCase CreateUseCase(Mock<IEmployeeWorkLogRepository> repository)
    {
        return new UpdateEmployeeWorkLogUseCase(repository.Object, new EmployeeWorkLogValidator());
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
}

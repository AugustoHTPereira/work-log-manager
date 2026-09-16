using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.EmployeeWorkLogs;

namespace WorkLogManager.Application.Tests.UseCases.EmployeeWorkLogs;

public class DeleteEmployeeWorkLogUseCaseTests
{
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

    [Fact]
    public async Task ExecuteAsync_ExistingWorkLog_RemovesOnlyThatWorkLog()
    {
        var employeeId = Guid.NewGuid();
        var workLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository.Setup(r => r.GetByIdAsync(workLog.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workLog);

        var useCase = new DeleteEmployeeWorkLogUseCase(workLogRepository.Object, CreateNotConfiguredSystemParameterRepository().Object);

        await useCase.ExecuteAsync(employeeId, workLog.Id);

        workLogRepository.Verify(r => r.DeleteAsync(workLog, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistingWorkLog_ThrowsNotFoundException()
    {
        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeWorkLog?)null);

        var useCase = new DeleteEmployeeWorkLogUseCase(workLogRepository.Object, CreateNotConfiguredSystemParameterRepository().Object);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid()));

        workLogRepository.Verify(r => r.DeleteAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WorkLogAlreadyLinkedToMonthClosing_ThrowsDomainExceptionAndDoesNotCallRepository()
    {
        var employeeId = Guid.NewGuid();
        var workLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId);
        workLog.MonthClosingId = Guid.NewGuid();

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository.Setup(r => r.GetByIdAsync(workLog.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workLog);

        var useCase = new DeleteEmployeeWorkLogUseCase(workLogRepository.Object, CreateNotConfiguredSystemParameterRepository().Object);

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(employeeId, workLog.Id));

        workLogRepository.Verify(r => r.DeleteAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WorkLogLinkedToMonthClosingWithAllowManageClosedWorkLogsTrue_DeletesNormally()
    {
        var employeeId = Guid.NewGuid();
        var workLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId, origin: WorkLogOrigin.Automatic);
        workLog.MonthClosingId = Guid.NewGuid();

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository.Setup(r => r.GetByIdAsync(workLog.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workLog);

        var useCase = new DeleteEmployeeWorkLogUseCase(workLogRepository.Object, CreateAllowManageClosedWorkLogsRepository(allow: true).Object);

        await useCase.ExecuteAsync(employeeId, workLog.Id);

        workLogRepository.Verify(r => r.DeleteAsync(workLog, It.IsAny<CancellationToken>()), Times.Once);
    }
}

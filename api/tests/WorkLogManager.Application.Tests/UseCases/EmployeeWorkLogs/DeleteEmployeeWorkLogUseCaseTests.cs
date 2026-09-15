using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.EmployeeWorkLogs;

namespace WorkLogManager.Application.Tests.UseCases.EmployeeWorkLogs;

public class DeleteEmployeeWorkLogUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ExistingWorkLog_RemovesOnlyThatWorkLog()
    {
        var employeeId = Guid.NewGuid();
        var workLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employeeId);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository.Setup(r => r.GetByIdAsync(workLog.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workLog);

        var useCase = new DeleteEmployeeWorkLogUseCase(workLogRepository.Object);

        await useCase.ExecuteAsync(employeeId, workLog.Id);

        workLogRepository.Verify(r => r.DeleteAsync(workLog, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistingWorkLog_ThrowsNotFoundException()
    {
        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeWorkLog?)null);

        var useCase = new DeleteEmployeeWorkLogUseCase(workLogRepository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid()));

        workLogRepository.Verify(r => r.DeleteAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

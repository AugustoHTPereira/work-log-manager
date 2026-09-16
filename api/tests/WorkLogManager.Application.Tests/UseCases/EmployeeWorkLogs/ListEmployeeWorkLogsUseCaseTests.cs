using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.EmployeeWorkLogs;

namespace WorkLogManager.Application.Tests.UseCases.EmployeeWorkLogs;

public class ListEmployeeWorkLogsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_EmployeeExists_ReturnsWorkLogsFromRepository()
    {
        var employee = EntityFactory.CreateEmployee();
        var workLog = EntityFactory.CreateEmployeeWorkLog(employeeId: employee.Id);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository
            .Setup(r => r.ListByEmployeeIdAsync(
                employee.Id,
                null,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeWorkLog> { workLog });

        var useCase = new ListEmployeeWorkLogsUseCase(employeeRepository.Object, workLogRepository.Object);

        var result = await useCase.ExecuteAsync(employee.Id);

        Assert.Single(result);
        Assert.Equal(workLog.Id, result[0].Id);
    }

    [Fact]
    public async Task ExecuteAsync_WithFilters_PassesThemToRepository()
    {
        var employee = EntityFactory.CreateEmployee();
        var startDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository
            .Setup(r => r.ListByEmployeeIdAsync(
                employee.Id,
                startDate,
                endDate,
                WorkLogType.Overtime,
                WorkLogOrigin.Manual,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeWorkLog>());

        var useCase = new ListEmployeeWorkLogsUseCase(employeeRepository.Object, workLogRepository.Object);

        await useCase.ExecuteAsync(employee.Id, startDate, endDate, WorkLogType.Overtime, WorkLogOrigin.Manual);

        workLogRepository.Verify(
            r => r.ListByEmployeeIdAsync(
                employee.Id,
                startDate,
                endDate,
                WorkLogType.Overtime,
                WorkLogOrigin.Manual,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeDoesNotExist_ThrowsNotFoundException()
    {
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();

        var useCase = new ListEmployeeWorkLogsUseCase(employeeRepository.Object, workLogRepository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));

        workLogRepository.Verify(
            r => r.ListByEmployeeIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<WorkLogType?>(),
                It.IsAny<WorkLogOrigin?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

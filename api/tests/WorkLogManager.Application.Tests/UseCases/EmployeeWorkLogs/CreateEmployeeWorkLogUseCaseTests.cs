using FluentValidation;
using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.EmployeeWorkLogs;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.EmployeeWorkLogs;

public class CreateEmployeeWorkLogUseCaseTests
{
    private static CreateEmployeeWorkLogUseCase CreateUseCase(
        Mock<IEmployeeRepository> employeeRepository,
        Mock<IEmployeeWorkLogRepository> workLogRepository)
    {
        return new CreateEmployeeWorkLogUseCase(employeeRepository.Object, workLogRepository.Object, new EmployeeWorkLogValidator());
    }

    [Fact]
    public async Task ExecuteAsync_ValidDates_CalculatesDurationFromStartAndEnd()
    {
        var employee = EntityFactory.CreateEmployee();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();

        var useCase = CreateUseCase(employeeRepository, workLogRepository);

        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(2);
        var input = EntityFactory.CreateEmployeeWorkLog(type: WorkLogType.Overtime, startDate: start, endDate: end);

        var result = await useCase.ExecuteAsync(employee.Id, input);

        Assert.Equal(7200, result.DurationSeconds);
        Assert.Equal(employee.Id, result.EmployeeId);
        workLogRepository.Verify(r => r.AddAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EndDateBeforeStartDate_ThrowsAndDoesNotCallRepository()
    {
        var employee = EntityFactory.CreateEmployee();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();

        var useCase = CreateUseCase(employeeRepository, workLogRepository);

        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var input = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start.AddHours(1));
        input.EndDate = start.AddHours(-1);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(employee.Id, input));

        workLogRepository.Verify(r => r.AddAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeDoesNotExist_ThrowsNotFoundException()
    {
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();

        var useCase = CreateUseCase(employeeRepository, workLogRepository);

        var start = DateTimeOffset.UtcNow;
        var input = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), input));
    }
}

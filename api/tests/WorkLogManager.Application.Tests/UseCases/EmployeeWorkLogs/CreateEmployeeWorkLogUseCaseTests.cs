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

    private static CreateEmployeeWorkLogUseCase CreateUseCase(
        Mock<IEmployeeRepository> employeeRepository,
        Mock<IEmployeeWorkLogRepository> workLogRepository,
        Mock<IMonthClosingRepository>? monthClosingRepository = null,
        Mock<ISystemParameterRepository>? systemParameterRepository = null)
    {
        return new CreateEmployeeWorkLogUseCase(
            employeeRepository.Object,
            workLogRepository.Object,
            (monthClosingRepository ?? CreateOpenMonthClosingRepository()).Object,
            (systemParameterRepository ?? CreateNotConfiguredSystemParameterRepository()).Object,
            new EmployeeWorkLogValidator());
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
    public async Task ExecuteAsync_ValidDates_SetsOriginToManual()
    {
        var employee = EntityFactory.CreateEmployee();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();

        var useCase = CreateUseCase(employeeRepository, workLogRepository);

        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var input = EntityFactory.CreateEmployeeWorkLog(
            type: WorkLogType.Overtime, startDate: start, endDate: start.AddHours(1), origin: WorkLogOrigin.Automatic);

        var result = await useCase.ExecuteAsync(employee.Id, input);

        Assert.Equal(WorkLogOrigin.Manual, result.Origin);
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

    [Fact]
    public async Task ExecuteAsync_StartDateFallsInAlreadyClosedMonth_ThrowsDomainExceptionAndDoesNotCallRepository()
    {
        var employee = EntityFactory.CreateEmployee();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();

        var monthClosingRepository = new Mock<IMonthClosingRepository>();
        monthClosingRepository
            .Setup(r => r.GetByMonthYearAsync(1, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntityFactory.CreateMonthClosing(month: 1, year: 2026));

        var useCase = CreateUseCase(employeeRepository, workLogRepository, monthClosingRepository);

        var start = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        var input = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start.AddHours(1));

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(employee.Id, input));

        workLogRepository.Verify(r => r.AddAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StartDateInClosedMonthWithAllowManageClosedWorkLogsTrue_CreatesNormally()
    {
        var employee = EntityFactory.CreateEmployee();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();

        var monthClosingRepository = new Mock<IMonthClosingRepository>();
        monthClosingRepository
            .Setup(r => r.GetByMonthYearAsync(1, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntityFactory.CreateMonthClosing(month: 1, year: 2026));

        var systemParameterRepository = new Mock<ISystemParameterRepository>();
        systemParameterRepository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AllowManageClosedWorkLogs, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[
                new SystemParameter
                {
                    Id = Guid.NewGuid(),
                    Param = SystemParameterName.AllowManageClosedWorkLogs,
                    Value = "true",
                    ValueType = SystemParameterValueType.Bool,
                },
            ]);

        var useCase = CreateUseCase(employeeRepository, workLogRepository, monthClosingRepository, systemParameterRepository);

        var start = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        var input = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start.AddHours(1));

        var result = await useCase.ExecuteAsync(employee.Id, input);

        Assert.NotNull(result);
        workLogRepository.Verify(r => r.AddAsync(It.IsAny<EmployeeWorkLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

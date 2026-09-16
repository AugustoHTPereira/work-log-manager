using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Results;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.MonthClosings;

namespace WorkLogManager.Application.Tests.UseCases.MonthClosings;

public class GenerateMonthClosingReportUseCaseTests
{
    private static (
        Mock<IMonthClosingRepository> MonthClosingRepository,
        Mock<IEmployeeWorkLogRepository> EmployeeWorkLogRepository,
        Mock<IEmployeeRepository> EmployeeRepository,
        Mock<IMonthClosingReportRenderer> ReportRenderer,
        GenerateMonthClosingReportUseCase UseCase) BuildUseCase()
    {
        var monthClosingRepository = new Mock<IMonthClosingRepository>();
        var employeeWorkLogRepository = new Mock<IEmployeeWorkLogRepository>();
        var employeeRepository = new Mock<IEmployeeRepository>();
        var reportRenderer = new Mock<IMonthClosingReportRenderer>();
        reportRenderer.Setup(r => r.Render(It.IsAny<MonthClosingReportData>())).Returns([1, 2, 3]);

        var useCase = new GenerateMonthClosingReportUseCase(
            monthClosingRepository.Object,
            employeeWorkLogRepository.Object,
            employeeRepository.Object,
            reportRenderer.Object);

        return (monthClosingRepository, employeeWorkLogRepository, employeeRepository, reportRenderer, useCase);
    }

    [Fact]
    public async Task ExecuteAsync_MonthClosingDoesNotExist_ThrowsNotFoundException()
    {
        var (monthClosingRepository, _, _, _, useCase) = BuildUseCase();
        monthClosingRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MonthClosing?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeWithNoWorkLogsInClosing_DoesNotAppearInReport()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var employeeWithLogs = EntityFactory.CreateEmployee(name: "Ana Souza", id: Guid.NewGuid());
        var employeeWithoutLogs = EntityFactory.CreateEmployee(name: "Bruno Lima", id: Guid.NewGuid());

        var workLog = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employeeWithLogs.Id,
            startDate: new DateTimeOffset(2026, 8, 3, 11, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 15, 0, 0, TimeSpan.Zero));

        var (monthClosingRepository, employeeWorkLogRepository, employeeRepository, reportRenderer, useCase) = BuildUseCase();

        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([workLog]);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([employeeWithLogs, employeeWithoutLogs]);

        MonthClosingReportData? capturedReportData = null;
        reportRenderer
            .Setup(r => r.Render(It.IsAny<MonthClosingReportData>()))
            .Callback<MonthClosingReportData>(data => capturedReportData = data)
            .Returns([1]);

        await useCase.ExecuteAsync(monthClosing.Id);

        Assert.NotNull(capturedReportData);
        Assert.Single(capturedReportData!.Employees);
        Assert.Equal("Ana Souza", capturedReportData.Employees[0].EmployeeName);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeWithAtLeastOneWorkLog_GeneratesRowForEveryDayInMonth()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var employee = EntityFactory.CreateEmployee(name: "Ana Souza", id: Guid.NewGuid());

        var workLog = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            startDate: new DateTimeOffset(2026, 8, 3, 11, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 15, 0, 0, TimeSpan.Zero));

        var (monthClosingRepository, employeeWorkLogRepository, employeeRepository, reportRenderer, useCase) = BuildUseCase();

        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([workLog]);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([employee]);

        MonthClosingReportData? capturedReportData = null;
        reportRenderer
            .Setup(r => r.Render(It.IsAny<MonthClosingReportData>()))
            .Callback<MonthClosingReportData>(data => capturedReportData = data)
            .Returns([1]);

        await useCase.ExecuteAsync(monthClosing.Id);

        var days = capturedReportData!.Employees[0].Days;
        Assert.Equal(DateTime.DaysInMonth(2026, 8), days.Count);

        var dayWithEntry = days.Single(d => d.Date == new DateOnly(2026, 8, 3));
        Assert.Single(dayWithEntry.Entries);

        var dayWithoutEntry = days.Single(d => d.Date == new DateOnly(2026, 8, 4));
        Assert.Empty(dayWithoutEntry.Entries);
        Assert.Equal(0, dayWithoutEntry.TotalSeconds);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleWorkLogsInSameDay_SumsDurationAndListsAllEntries()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var employee = EntityFactory.CreateEmployee(name: "Ana Souza", id: Guid.NewGuid());

        var regularAttendance = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            type: WorkLogType.RegularAttendance,
            startDate: new DateTimeOffset(2026, 8, 3, 11, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 15, 0, 0, TimeSpan.Zero));

        var overtime = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            type: WorkLogType.Overtime,
            startDate: new DateTimeOffset(2026, 8, 3, 15, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 17, 0, 0, TimeSpan.Zero));

        var (monthClosingRepository, employeeWorkLogRepository, employeeRepository, reportRenderer, useCase) = BuildUseCase();

        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([regularAttendance, overtime]);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([employee]);

        MonthClosingReportData? capturedReportData = null;
        reportRenderer
            .Setup(r => r.Render(It.IsAny<MonthClosingReportData>()))
            .Callback<MonthClosingReportData>(data => capturedReportData = data)
            .Returns([1]);

        await useCase.ExecuteAsync(monthClosing.Id);

        var day = capturedReportData!.Employees[0].Days.Single(d => d.Date == new DateOnly(2026, 8, 3));

        Assert.Equal(2, day.Entries.Count);
        Assert.Equal(regularAttendance.DurationSeconds + overtime.DurationSeconds, day.TotalSeconds);
        Assert.Contains(day.Entries, e => e.Type == WorkLogType.RegularAttendance);
        Assert.Contains(day.Entries, e => e.Type == WorkLogType.Overtime);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleNotesInSameDay_ConcatenatesWithSemicolonAndIgnoresEmpty()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var employee = EntityFactory.CreateEmployee(name: "Ana Souza", id: Guid.NewGuid());

        var firstWorkLog = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            startDate: new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero),
            note: "nota1");

        var secondWorkLog = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            startDate: new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero),
            note: "nota2");

        var thirdWorkLog = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            startDate: new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 13, 0, 0, TimeSpan.Zero),
            note: "  ");

        var (monthClosingRepository, employeeWorkLogRepository, employeeRepository, reportRenderer, useCase) = BuildUseCase();

        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstWorkLog, secondWorkLog, thirdWorkLog]);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([employee]);

        MonthClosingReportData? capturedReportData = null;
        reportRenderer
            .Setup(r => r.Render(It.IsAny<MonthClosingReportData>()))
            .Callback<MonthClosingReportData>(data => capturedReportData = data)
            .Returns([1]);

        await useCase.ExecuteAsync(monthClosing.Id);

        var day = capturedReportData!.Employees[0].Days.Single(d => d.Date == new DateOnly(2026, 8, 3));

        Assert.Equal("nota1; nota2", day.ObservationText);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleEmployees_OrdersAlphabeticallyByNameAndEntriesByStart()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 8, year: 2026);
        var employeeB = EntityFactory.CreateEmployee(name: "Bruno Lima", id: Guid.NewGuid());
        var employeeA = EntityFactory.CreateEmployee(name: "Ana Souza", id: Guid.NewGuid());

        var workLogB = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employeeB.Id,
            startDate: new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero));

        var laterWorkLogA = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employeeA.Id,
            startDate: new DateTimeOffset(2026, 8, 3, 14, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 16, 0, 0, TimeSpan.Zero));

        var earlierWorkLogA = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employeeA.Id,
            startDate: new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero));

        var (monthClosingRepository, employeeWorkLogRepository, employeeRepository, reportRenderer, useCase) = BuildUseCase();

        monthClosingRepository.Setup(r => r.GetByIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthClosing);
        employeeWorkLogRepository.Setup(r => r.ListByMonthClosingIdAsync(monthClosing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([workLogB, laterWorkLogA, earlierWorkLogA]);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([employeeB, employeeA]);

        MonthClosingReportData? capturedReportData = null;
        reportRenderer
            .Setup(r => r.Render(It.IsAny<MonthClosingReportData>()))
            .Callback<MonthClosingReportData>(data => capturedReportData = data)
            .Returns([1]);

        await useCase.ExecuteAsync(monthClosing.Id);

        Assert.Equal(["Ana Souza", "Bruno Lima"], capturedReportData!.Employees.Select(e => e.EmployeeName));

        var dayA = capturedReportData.Employees.Single(e => e.EmployeeName == "Ana Souza").Days
            .Single(d => d.Date == new DateOnly(2026, 8, 3));

        Assert.Equal([earlierWorkLogA.StartDate, laterWorkLogA.StartDate], dayA.Entries.Select(e => e.Start));
    }
}

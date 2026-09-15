using FluentValidation;
using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.MonthClosings;
using WorkLogManager.Application.UseCases.SystemSettings;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.MonthClosings;

/// <summary>
/// Always returns the same fixed value, regardless of the requested range. Produces neutral
/// (zero) offsets throughout <see cref="WorkLogGenerationService"/>, which keeps the
/// generated periods deterministic and easy to assert on in <see cref="CloseMonthUseCaseTests"/>.
/// </summary>
public class FixedRandomProvider : IRandomProvider
{
    private readonly int _value;

    public FixedRandomProvider(int value = 0)
    {
        _value = value;
    }

    public int NextInt(int minInclusive, int maxInclusive) => _value;
}

public class CloseMonthUseCaseTests
{
    private static int CountBusinessDays(int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var count = 0;
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                count++;
            }
        }

        return count;
    }

    private static (
        CloseMonthUseCase UseCase,
        Mock<IEmployeeRepository> EmployeeRepository,
        Mock<IEmployeeWorkLogRepository> WorkLogRepository,
        Mock<IMonthClosingRepository> MonthClosingRepository,
        Mock<ISystemSettingsRepository> SystemSettingsRepository) CreateSut(int randomValue = 0)
    {
        var employeeRepository = new Mock<IEmployeeRepository>();
        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        var monthClosingRepository = new Mock<IMonthClosingRepository>();
        var systemSettingsRepository = new Mock<ISystemSettingsRepository>();

        monthClosingRepository
            .Setup(r => r.GetByMonthYearAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MonthClosing?)null);

        workLogRepository
            .Setup(r => r.ListByTypeAndDateRangeAsync(
                It.IsAny<WorkLogType>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeWorkLog>());

        var getSystemSettingsUseCase = new GetSystemSettingsUseCase(systemSettingsRepository.Object);
        var workLogGenerationService = new WorkLogGenerationService(new FixedRandomProvider(randomValue));

        var useCase = new CloseMonthUseCase(
            employeeRepository.Object,
            workLogRepository.Object,
            monthClosingRepository.Object,
            getSystemSettingsUseCase,
            workLogGenerationService,
            new MonthClosingValidator());

        return (useCase, employeeRepository, workLogRepository, monthClosingRepository, systemSettingsRepository);
    }

    [Fact]
    public async Task ExecuteAsync_FebruaryTwentyEight_GeneratesRecordsForEveryBusinessDay()
    {
        var (useCase, employeeRepository, workLogRepository, _, systemSettingsRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee(dailyWorkHours: 8m);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        systemSettingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EntityFactory.CreateSystemSettings());

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 2, year: 2026);
        var result = await useCase.ExecuteAsync(input);

        var businessDays = CountBusinessDays(2026, 2);
        Assert.NotNull(addedWorkLogs);
        // 8h journey -> 2 RegularAttendance + 1 Break per business day.
        Assert.Equal(businessDays * 3, addedWorkLogs!.Count);
        Assert.Single(result.Summaries);
        Assert.Equal(businessDays, result.Summaries[0].GeneratedCount);
        Assert.Equal(0, result.Summaries[0].SkippedCount);
        Assert.All(addedWorkLogs, w => Assert.Equal(result.MonthClosing.Id, w.MonthClosingId));
    }

    [Theory]
    [InlineData(2026, 4)] // 30 days
    [InlineData(2020, 1)] // 31 days
    public async Task ExecuteAsync_ThirtyOrThirtyOneDayMonths_GeneratesExpectedBusinessDayCount(int year, int month)
    {
        var (useCase, employeeRepository, workLogRepository, _, systemSettingsRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee(dailyWorkHours: 3m);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        systemSettingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EntityFactory.CreateSystemSettings());

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: month, year: year);
        var result = await useCase.ExecuteAsync(input);

        var businessDays = CountBusinessDays(year, month);
        // 3h journey -> a single RegularAttendance, no Break.
        Assert.Equal(businessDays, addedWorkLogs!.Count);
        Assert.Equal(businessDays, result.Summaries[0].GeneratedCount);
    }

    [Fact]
    public async Task ExecuteAsync_AllDaysAlreadyHaveRegularAttendance_SkipsEveryDayButStillCreatesMonthClosing()
    {
        var (useCase, employeeRepository, workLogRepository, monthClosingRepository, systemSettingsRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee(dailyWorkHours: 8m);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        systemSettingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EntityFactory.CreateSystemSettings());

        var year = 2026;
        var month = 3;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var existing = new List<EmployeeWorkLog>();
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            existing.Add(EntityFactory.CreateEmployeeWorkLog(
                employeeId: employee.Id,
                type: WorkLogType.RegularAttendance,
                startDate: new DateTimeOffset(year, month, day, 7, 0, 0, TimeSpan.Zero),
                endDate: new DateTimeOffset(year, month, day, 15, 0, 0, TimeSpan.Zero)));
        }

        workLogRepository
            .Setup(r => r.ListByTypeAndDateRangeAsync(
                WorkLogType.RegularAttendance, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: month, year: year);
        var result = await useCase.ExecuteAsync(input);

        var businessDays = CountBusinessDays(year, month);
        Assert.Empty(addedWorkLogs!);
        Assert.Equal(0, result.Summaries[0].GeneratedCount);
        Assert.Equal(businessDays, result.Summaries[0].SkippedCount);
        monthClosingRepository.Verify(r => r.AddAsync(It.IsAny<MonthClosing>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DayAlreadyHasRegularAttendance_SkipsBothRegularAttendanceAndBreakForThatDay()
    {
        var (useCase, employeeRepository, workLogRepository, _, systemSettingsRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee(dailyWorkHours: 8m);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        systemSettingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EntityFactory.CreateSystemSettings());

        // February 2026: day 2 is a Monday (first business day).
        var firstBusinessDay = new DateTimeOffset(2026, 2, 2, 7, 0, 0, TimeSpan.Zero);
        var existing = new List<EmployeeWorkLog>
        {
            EntityFactory.CreateEmployeeWorkLog(
                employeeId: employee.Id,
                type: WorkLogType.RegularAttendance,
                startDate: firstBusinessDay,
                endDate: firstBusinessDay.AddHours(8)),
        };

        workLogRepository
            .Setup(r => r.ListByTypeAndDateRangeAsync(
                WorkLogType.RegularAttendance, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 2, year: 2026);
        var result = await useCase.ExecuteAsync(input);

        Assert.DoesNotContain(addedWorkLogs!, w => w.StartDate.Date == firstBusinessDay.Date);
        Assert.Equal(1, result.Summaries[0].SkippedCount);

        workLogRepository.Verify(
            r => r.ListByTypeAndDateRangeAsync(
                WorkLogType.RegularAttendance, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        workLogRepository.Verify(
            r => r.ListByTypeAndDateRangeAsync(
                WorkLogType.Break, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_MonthOutOfRange_ThrowsValidationExceptionAndCallsNoRepository()
    {
        var (useCase, employeeRepository, workLogRepository, monthClosingRepository, _) = CreateSut();

        var input = EntityFactory.CreateMonthClosing(month: 13, year: 2020);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(input));

        employeeRepository.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        monthClosingRepository.Verify(r => r.AddAsync(It.IsAny<MonthClosing>(), It.IsAny<CancellationToken>()), Times.Never);
        workLogRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CurrentOrFutureMonth_ThrowsValidationException()
    {
        var (useCase, _, _, _, _) = CreateSut();

        var now = DateTimeOffset.UtcNow;
        var input = EntityFactory.CreateMonthClosing(month: now.Month, year: now.Year);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(input));
    }

    [Fact]
    public async Task ExecuteAsync_MonthAlreadyClosed_ThrowsDomainExceptionAndDoesNotPersistAnything()
    {
        var (useCase, employeeRepository, workLogRepository, monthClosingRepository, _) = CreateSut();

        monthClosingRepository
            .Setup(r => r.GetByMonthYearAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntityFactory.CreateMonthClosing(month: 1, year: 2020));

        var input = EntityFactory.CreateMonthClosing(month: 1, year: 2020);

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(input));

        employeeRepository.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        monthClosingRepository.Verify(r => r.AddAsync(It.IsAny<MonthClosing>(), It.IsAny<CancellationToken>()), Times.Never);
        workLogRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeWithoutDailyWorkHours_UsesSystemSettingsDefault()
    {
        var (useCase, employeeRepository, workLogRepository, _, systemSettingsRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee(dailyWorkHours: null);
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        systemSettingsRepository
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntityFactory.CreateSystemSettings(defaultDailyWorkHours: 8m));

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 3, year: 2026);
        await useCase.ExecuteAsync(input);

        var businessDays = CountBusinessDays(2026, 3);
        // 8h (system default) -> 3 records/day (2 RegularAttendance + 1 Break).
        Assert.Equal(businessDays * 3, addedWorkLogs!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_NoEmployees_CreatesMonthClosingWithEmptySummaries()
    {
        var (useCase, employeeRepository, workLogRepository, monthClosingRepository, systemSettingsRepository) = CreateSut();

        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        systemSettingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EntityFactory.CreateSystemSettings());

        var input = EntityFactory.CreateMonthClosing(month: 3, year: 2026);
        var result = await useCase.ExecuteAsync(input);

        Assert.Empty(result.Summaries);
        monthClosingRepository.Verify(r => r.AddAsync(It.IsAny<MonthClosing>(), It.IsAny<CancellationToken>()), Times.Once);
        workLogRepository.Verify(
            r => r.AddRangeAsync(It.Is<IEnumerable<EmployeeWorkLog>>(logs => !logs.Any()), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

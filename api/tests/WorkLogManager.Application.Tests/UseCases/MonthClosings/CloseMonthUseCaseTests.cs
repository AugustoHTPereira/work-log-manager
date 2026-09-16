using FluentValidation;
using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.MonthClosings;
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

    /// <summary>
    /// Builds a full week (Monday-Friday) of 8h general periods (08:00-12:00 and 13:00-17:00),
    /// matching the previous hardcoded "business days = weekdays" behavior used by most tests.
    /// </summary>
    private static List<WorkSchedulePeriod> BuildGeneralWeekdayPeriods(decimal dailyWorkHours = 8m)
    {
        var weekdays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        };

        var periods = new List<WorkSchedulePeriod>();
        foreach (var dayOfWeek in weekdays)
        {
            if (dailyWorkHours <= 4m)
            {
                periods.Add(EntityFactory.CreateWorkSchedulePeriod(
                    employeeId: null,
                    dayOfWeek: dayOfWeek,
                    startTime: new TimeOnly(8, 0),
                    endTime: new TimeOnly(8, 0).AddHours((double)dailyWorkHours)));
            }
            else
            {
                periods.Add(EntityFactory.CreateWorkSchedulePeriod(
                    employeeId: null,
                    dayOfWeek: dayOfWeek,
                    startTime: new TimeOnly(8, 0),
                    endTime: new TimeOnly(12, 0)));
                periods.Add(EntityFactory.CreateWorkSchedulePeriod(
                    employeeId: null,
                    dayOfWeek: dayOfWeek,
                    startTime: new TimeOnly(13, 0),
                    endTime: new TimeOnly(13, 0).AddHours((double)(dailyWorkHours - 4m))));
            }
        }

        return periods;
    }

    private static (
        CloseMonthUseCase UseCase,
        Mock<IEmployeeRepository> EmployeeRepository,
        Mock<IEmployeeWorkLogRepository> WorkLogRepository,
        Mock<IMonthClosingRepository> MonthClosingRepository,
        Mock<IWorkSchedulePeriodRepository> WorkSchedulePeriodRepository) CreateSut(
            int randomValue = 0, string[]? autoWorkLogTypesValues = null)
    {
        var employeeRepository = new Mock<IEmployeeRepository>();
        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        var monthClosingRepository = new Mock<IMonthClosingRepository>();
        var workSchedulePeriodRepository = new Mock<IWorkSchedulePeriodRepository>();
        var systemParameterRepository = new Mock<ISystemParameterRepository>();

        monthClosingRepository
            .Setup(r => r.GetByMonthYearAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MonthClosing?)null);

        systemParameterRepository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AutoWorkLogTypes, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)(autoWorkLogTypesValues ?? [])
                .Select(value => new SystemParameter
                {
                    Id = Guid.NewGuid(),
                    Param = SystemParameterName.AutoWorkLogTypes,
                    Value = value,
                    ValueType = SystemParameterValueType.Array,
                })
                .ToList());

        workLogRepository
            .Setup(r => r.ListByTypeAndDateRangeAsync(
                It.IsAny<WorkLogType>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeWorkLog>());

        workLogRepository
            .Setup(r => r.ListUnclosedByDateRangeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeWorkLog>());

        workLogRepository
            .Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var workLogGenerationService = new WorkLogGenerationService(new FixedRandomProvider(randomValue));

        var useCase = new CloseMonthUseCase(
            employeeRepository.Object,
            workLogRepository.Object,
            monthClosingRepository.Object,
            workSchedulePeriodRepository.Object,
            systemParameterRepository.Object,
            workLogGenerationService,
            new MonthClosingValidator());

        return (useCase, employeeRepository, workLogRepository, monthClosingRepository, workSchedulePeriodRepository);
    }

    [Fact]
    public async Task ExecuteAsync_FebruaryTwentyEight_GeneratesRecordsForEveryBusinessDay()
    {
        // No AutoWorkLogTypes configured -> defaults to only RegularAttendance (no Break).
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGeneralWeekdayPeriods());

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 2, year: 2026);
        var result = await useCase.ExecuteAsync(input);

        var businessDays = CountBusinessDays(2026, 2);
        Assert.NotNull(addedWorkLogs);
        // 8h journey -> 2 RegularAttendance periods per business day, no Break (default fallback).
        Assert.Equal(businessDays * 2, addedWorkLogs!.Count);
        Assert.All(addedWorkLogs, w => Assert.Equal(WorkLogType.RegularAttendance, w.Type));
        Assert.Single(result.Summaries);
        Assert.Equal(businessDays, result.Summaries[0].GeneratedCount);
        Assert.Equal(0, result.Summaries[0].SkippedCount);
        Assert.All(addedWorkLogs, w => Assert.Equal(result.MonthClosing.Id, w.MonthClosingId));
        Assert.All(addedWorkLogs, w => Assert.Equal(WorkLogOrigin.Automatic, w.Origin));
    }

    [Fact]
    public async Task ExecuteAsync_AutoWorkLogTypesConfiguredWithBoth_GeneratesRegularAttendanceAndBreak()
    {
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) =
            CreateSut(autoWorkLogTypesValues: ["RegularAttendance", "Break"]);

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGeneralWeekdayPeriods());

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 2, year: 2026);
        await useCase.ExecuteAsync(input);

        var businessDays = CountBusinessDays(2026, 2);
        // 8h journey -> 2 RegularAttendance + 1 Break per business day.
        Assert.Equal(businessDays * 3, addedWorkLogs!.Count);
        Assert.Contains(addedWorkLogs, w => w.Type == WorkLogType.Break);
    }

    [Fact]
    public async Task ExecuteAsync_AutoWorkLogTypesConfiguredWithOnlyBreak_DoesNotGenerateRegularAttendance()
    {
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) =
            CreateSut(autoWorkLogTypesValues: ["Break"]);

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGeneralWeekdayPeriods());

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 2, year: 2026);
        await useCase.ExecuteAsync(input);

        var businessDays = CountBusinessDays(2026, 2);
        Assert.Equal(businessDays, addedWorkLogs!.Count);
        Assert.All(addedWorkLogs, w => Assert.Equal(WorkLogType.Break, w.Type));
        Assert.DoesNotContain(addedWorkLogs, w => w.Type == WorkLogType.RegularAttendance);
    }

    [Theory]
    [InlineData(2026, 4)] // 30 days
    [InlineData(2020, 1)] // 31 days
    public async Task ExecuteAsync_ThirtyOrThirtyOneDayMonths_GeneratesExpectedBusinessDayCount(int year, int month)
    {
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGeneralWeekdayPeriods(dailyWorkHours: 3m));

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
        var (useCase, employeeRepository, workLogRepository, monthClosingRepository, workSchedulePeriodRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGeneralWeekdayPeriods());

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
                startDate: new DateTimeOffset(year, month, day, 7, 0, 0, TimeSpan.FromHours(-3)),
                endDate: new DateTimeOffset(year, month, day, 15, 0, 0, TimeSpan.FromHours(-3))));
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
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGeneralWeekdayPeriods());

        // February 2026: day 2 is a Monday (first business day).
        var firstBusinessDay = new DateTimeOffset(2026, 2, 2, 7, 0, 0, TimeSpan.FromHours(-3));
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
    public async Task ExecuteAsync_DayAlreadyHasRegularAttendance_ClassifiesByBusinessLocalDate_NotUtcDate()
    {
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGeneralWeekdayPeriods());

        // March 2026: day 31 is a Tuesday (business day) and the last day of the month.
        // 23:30 business-local time on March 31 is 02:30 UTC on April 1 - if classified by
        // UTC date this would wrongly "leak" into April and fail to match March 31.
        var lastDayLocalLateNight = new DateTimeOffset(2026, 3, 31, 23, 30, 0, TimeSpan.FromHours(-3));
        var existing = new List<EmployeeWorkLog>
        {
            EntityFactory.CreateEmployeeWorkLog(
                employeeId: employee.Id,
                type: WorkLogType.RegularAttendance,
                startDate: lastDayLocalLateNight,
                endDate: lastDayLocalLateNight.AddHours(1)),
        };

        DateTimeOffset? capturedRangeStart = null;
        DateTimeOffset? capturedRangeEnd = null;
        workLogRepository
            .Setup(r => r.ListByTypeAndDateRangeAsync(
                WorkLogType.RegularAttendance, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<WorkLogType, DateTimeOffset, DateTimeOffset, CancellationToken>((_, start, end, _) =>
            {
                capturedRangeStart = start;
                capturedRangeEnd = end;
            })
            .ReturnsAsync(existing);

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 3, year: 2026);
        var result = await useCase.ExecuteAsync(input);

        var businessDays = CountBusinessDays(2026, 3);
        Assert.Equal(1, result.Summaries[0].SkippedCount);
        Assert.Equal(businessDays - 1, result.Summaries[0].GeneratedCount);
        Assert.DoesNotContain(addedWorkLogs!, w => w.StartDate.Date == new DateTime(2026, 3, 31));

        Assert.NotNull(capturedRangeStart);
        Assert.NotNull(capturedRangeEnd);
        Assert.InRange(lastDayLocalLateNight, capturedRangeStart!.Value, capturedRangeEnd!.Value - TimeSpan.FromTicks(1));
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
    public async Task ExecuteAsync_EmployeeWithOwnPeriods_UsesEmployeeHoursInsteadOfGeneral()
    {
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);

        var allPeriods = BuildGeneralWeekdayPeriods(dailyWorkHours: 8m);
        // Employee has their own (shorter, 3h) schedule for weekdays - should override general entirely.
        allPeriods.AddRange(new[]
        {
            EntityFactory.CreateWorkSchedulePeriod(employeeId: employee.Id, dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(11, 0)),
            EntityFactory.CreateWorkSchedulePeriod(employeeId: employee.Id, dayOfWeek: DayOfWeek.Tuesday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(11, 0)),
            EntityFactory.CreateWorkSchedulePeriod(employeeId: employee.Id, dayOfWeek: DayOfWeek.Wednesday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(11, 0)),
            EntityFactory.CreateWorkSchedulePeriod(employeeId: employee.Id, dayOfWeek: DayOfWeek.Thursday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(11, 0)),
            EntityFactory.CreateWorkSchedulePeriod(employeeId: employee.Id, dayOfWeek: DayOfWeek.Friday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(11, 0)),
        });

        workSchedulePeriodRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(allPeriods);

        List<EmployeeWorkLog>? addedWorkLogs = null;
        workLogRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => addedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 3, year: 2026);
        await useCase.ExecuteAsync(input);

        var businessDays = CountBusinessDays(2026, 3);
        // 3h journey -> a single RegularAttendance per business day, no Break.
        Assert.Equal(businessDays, addedWorkLogs!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_DayWithNoApplicablePeriod_IsSkippedWithoutCountingAsSkipped()
    {
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        // No periods at all -> every day is a day off.
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WorkSchedulePeriod>());

        var input = EntityFactory.CreateMonthClosing(month: 3, year: 2026);
        var result = await useCase.ExecuteAsync(input);

        Assert.Equal(0, result.Summaries[0].GeneratedCount);
        Assert.Equal(0, result.Summaries[0].SkippedCount);
    }

    [Fact]
    public async Task ExecuteAsync_NoEmployees_CreatesMonthClosingWithEmptySummaries()
    {
        var (useCase, employeeRepository, workLogRepository, monthClosingRepository, workSchedulePeriodRepository) = CreateSut();

        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildGeneralWeekdayPeriods());

        var input = EntityFactory.CreateMonthClosing(month: 3, year: 2026);
        var result = await useCase.ExecuteAsync(input);

        Assert.Empty(result.Summaries);
        monthClosingRepository.Verify(r => r.AddAsync(It.IsAny<MonthClosing>(), It.IsAny<CancellationToken>()), Times.Once);
        workLogRepository.Verify(
            r => r.AddRangeAsync(It.Is<IEnumerable<EmployeeWorkLog>>(logs => !logs.Any()), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UnclosedManualWorkLogsOfAnyTypeInTheMonth_AreAssociatedWithTheNewClosing()
    {
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        // No effective schedule -> no auto-generated RegularAttendance/Break records, isolating
        // this test to the "associate pre-existing work logs" behavior.
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WorkSchedulePeriod>());

        var overtime = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            type: WorkLogType.Overtime,
            startDate: new DateTimeOffset(2026, 3, 10, 19, 0, 0, TimeSpan.FromHours(-3)),
            endDate: new DateTimeOffset(2026, 3, 10, 20, 0, 0, TimeSpan.FromHours(-3)));
        var absence = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            type: WorkLogType.Absence,
            startDate: new DateTimeOffset(2026, 3, 15, 8, 0, 0, TimeSpan.FromHours(-3)),
            endDate: new DateTimeOffset(2026, 3, 15, 17, 0, 0, TimeSpan.FromHours(-3)));

        workLogRepository
            .Setup(r => r.ListUnclosedByDateRangeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([overtime, absence]);

        IEnumerable<EmployeeWorkLog>? updatedWorkLogs = null;
        workLogRepository
            .Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => updatedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 3, year: 2026);
        var result = await useCase.ExecuteAsync(input);

        Assert.NotNull(updatedWorkLogs);
        Assert.Equal(2, updatedWorkLogs!.Count());
        Assert.All(updatedWorkLogs, w => Assert.Equal(result.MonthClosing.Id, w.MonthClosingId));
        Assert.Contains(updatedWorkLogs, w => w.Id == overtime.Id);
        Assert.Contains(updatedWorkLogs, w => w.Id == absence.Id);
    }

    [Fact]
    public async Task ExecuteAsync_QueriesOnlyUnclosedWorkLogsWithinTheClosingRange()
    {
        var (useCase, employeeRepository, workLogRepository, _, workSchedulePeriodRepository) = CreateSut();

        var employee = EntityFactory.CreateEmployee();
        employeeRepository.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        workSchedulePeriodRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WorkSchedulePeriod>());

        // Already linked to another closing: the repository query (mocked here to mimic the
        // MonthClosingId == null filter) would exclude it, so it must not reach UpdateRangeAsync.
        var alreadyClosed = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            type: WorkLogType.Overtime,
            startDate: new DateTimeOffset(2026, 3, 10, 19, 0, 0, TimeSpan.FromHours(-3)),
            endDate: new DateTimeOffset(2026, 3, 10, 20, 0, 0, TimeSpan.FromHours(-3)));
        alreadyClosed.MonthClosingId = Guid.NewGuid();

        // Outside the March range: excluded by the date-range filter mocked below.
        var outsideRange = EntityFactory.CreateEmployeeWorkLog(
            employeeId: employee.Id,
            type: WorkLogType.Overtime,
            startDate: new DateTimeOffset(2026, 4, 1, 19, 0, 0, TimeSpan.FromHours(-3)),
            endDate: new DateTimeOffset(2026, 4, 1, 20, 0, 0, TimeSpan.FromHours(-3)));

        DateTimeOffset? capturedRangeStart = null;
        DateTimeOffset? capturedRangeEnd = null;
        workLogRepository
            .Setup(r => r.ListUnclosedByDateRangeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, DateTimeOffset, CancellationToken>((start, end, _) =>
            {
                capturedRangeStart = start;
                capturedRangeEnd = end;
            })
            // Simulates the real repository's MonthClosingId == null + date-range filter:
            // neither alreadyClosed nor outsideRange would be returned in production.
            .ReturnsAsync(Array.Empty<EmployeeWorkLog>());

        IEnumerable<EmployeeWorkLog>? updatedWorkLogs = null;
        workLogRepository
            .Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<EmployeeWorkLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EmployeeWorkLog>, CancellationToken>((logs, _) => updatedWorkLogs = logs.ToList())
            .Returns(Task.CompletedTask);

        var input = EntityFactory.CreateMonthClosing(month: 3, year: 2026);
        await useCase.ExecuteAsync(input);

        Assert.NotNull(updatedWorkLogs);
        Assert.Empty(updatedWorkLogs!);
        Assert.NotNull(capturedRangeStart);
        Assert.NotNull(capturedRangeEnd);
        Assert.True(alreadyClosed.StartDate >= capturedRangeStart!.Value && alreadyClosed.StartDate < capturedRangeEnd!.Value);
        Assert.False(outsideRange.StartDate >= capturedRangeStart!.Value && outsideRange.StartDate < capturedRangeEnd!.Value);
    }
}

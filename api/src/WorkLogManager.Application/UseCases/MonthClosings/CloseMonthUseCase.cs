using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Results;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.MonthClosings;

/// <summary>
/// Closes a fully completed past calendar month by automatically generating, for every day and
/// every employee that has an effective work schedule for that day (see
/// <see cref="WorkScheduleResolver"/>), whichever of <see cref="WorkLogType.RegularAttendance"/>
/// and <see cref="WorkLogType.Break"/> is enabled by the <c>AutoWorkLogTypes</c> system
/// parameter (see <see cref="AutoWorkLogTypesParser"/>; defaults to only
/// <see cref="WorkLogType.RegularAttendance"/> when not configured). Days with no effective
/// schedule (neither employee-specific nor general) are treated as days off and skipped without
/// counting toward <see cref="EmployeeWorkLogGenerationSummary.SkippedCount"/>. In addition, every pre-existing
/// <see cref="EmployeeWorkLog"/> (of any <see cref="WorkLogType"/>, including manually created
/// ones such as <see cref="WorkLogType.Overtime"/>/<see cref="WorkLogType.Absence"/>) whose
/// <see cref="EmployeeWorkLog.StartDate"/> falls within the month being closed and that is not
/// yet linked to a <see cref="MonthClosing"/> gets its
/// <see cref="EmployeeWorkLog.MonthClosingId"/> set to this closing, "freezing" it so it can no
/// longer be edited or deleted (see <see cref="UseCases.EmployeeWorkLogs.UpdateEmployeeWorkLogUseCase"/>
/// and <see cref="UseCases.EmployeeWorkLogs.DeleteEmployeeWorkLogUseCase"/>).
/// </summary>
public class CloseMonthUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;
    private readonly IMonthClosingRepository _monthClosingRepository;
    private readonly IWorkSchedulePeriodRepository _workSchedulePeriodRepository;
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly WorkLogGenerationService _workLogGenerationService;
    private readonly IValidator<MonthClosing> _validator;

    public CloseMonthUseCase(
        IEmployeeRepository employeeRepository,
        IEmployeeWorkLogRepository employeeWorkLogRepository,
        IMonthClosingRepository monthClosingRepository,
        IWorkSchedulePeriodRepository workSchedulePeriodRepository,
        ISystemParameterRepository systemParameterRepository,
        WorkLogGenerationService workLogGenerationService,
        IValidator<MonthClosing> validator)
    {
        _employeeRepository = employeeRepository;
        _employeeWorkLogRepository = employeeWorkLogRepository;
        _monthClosingRepository = monthClosingRepository;
        _workSchedulePeriodRepository = workSchedulePeriodRepository;
        _systemParameterRepository = systemParameterRepository;
        _workLogGenerationService = workLogGenerationService;
        _validator = validator;
    }

    public async Task<MonthClosingResult> ExecuteAsync(MonthClosing input, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(input, cancellationToken);

        var existingClosing = await _monthClosingRepository.GetByMonthYearAsync(input.Month, input.Year, cancellationToken);
        if (existingClosing is not null)
        {
            throw new DomainException($"Month {input.Month:00}/{input.Year} has already been closed.");
        }

        var autoWorkLogTypeRows = await _systemParameterRepository.ListByParamAsync(
            SystemParameterName.AutoWorkLogTypes, cancellationToken);
        var enabledWorkLogTypes = AutoWorkLogTypesParser.Parse(autoWorkLogTypeRows);

        var employees = await _employeeRepository.ListAllAsync(cancellationToken);
        var allPeriods = await _workSchedulePeriodRepository.ListAllAsync(cancellationToken);

        var generalPeriods = allPeriods.Where(p => p.EmployeeId is null).ToList();
        var periodsByEmployeeId = allPeriods
            .Where(p => p.EmployeeId is not null)
            .GroupBy(p => p.EmployeeId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<WorkSchedulePeriod>)g.ToList());

        var rangeStart = BusinessTimeZone.ToInstant(new DateTime(input.Year, input.Month, 1, 0, 0, 0, DateTimeKind.Unspecified));
        var rangeEnd = rangeStart.AddMonths(1);

        var existingRegularAttendances = await _employeeWorkLogRepository.ListByTypeAndDateRangeAsync(
            WorkLogType.RegularAttendance, rangeStart, rangeEnd, cancellationToken);

        var existingAbsences = await _employeeWorkLogRepository.ListByTypeAndDateRangeAsync(
            WorkLogType.Absence, rangeStart, rangeEnd, cancellationToken);

        var existingDays = existingRegularAttendances.Concat(existingAbsences)
            .Select(w => (EmployeeId: w.EmployeeId, Date: BusinessTimeZone.ToBusinessDate(w.StartDate)))
            .ToHashSet();

        var now = DateTimeOffset.UtcNow;
        var monthClosing = new MonthClosing
        {
            Id = Guid.NewGuid(),
            Month = input.Month,
            Year = input.Year,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var workLogsToCreate = new List<EmployeeWorkLog>();
        var summaries = new List<EmployeeWorkLogGenerationSummary>();
        var daysInMonth = DateTime.DaysInMonth(input.Year, input.Month);

        foreach (var employee in employees)
        {
            var employeePeriods = periodsByEmployeeId.GetValueOrDefault(employee.Id, []);
            var generatedCount = 0;
            var skippedCount = 0;

            for (var day = 1; day <= daysInMonth; day++)
            {
                var date = new DateOnly(input.Year, input.Month, day);

                var effectiveDailyWorkHours = WorkScheduleResolver.GetEffectiveDailyWorkHours(
                    date.DayOfWeek, generalPeriods, employeePeriods);

                if (effectiveDailyWorkHours == 0m)
                {
                    continue;
                }

                if (existingDays.Contains((employee.Id, date)))
                {
                    skippedCount++;
                    continue;
                }

                var generatedWorkday = _workLogGenerationService.GenerateWorkday(date, effectiveDailyWorkHours);

                if (enabledWorkLogTypes.Contains(WorkLogType.RegularAttendance))
                {
                    foreach (var period in generatedWorkday.RegularAttendancePeriods)
                    {
                        workLogsToCreate.Add(BuildWorkLog(employee.Id, monthClosing.Id, WorkLogType.RegularAttendance, period.Start, period.End, now));
                    }
                }

                if (enabledWorkLogTypes.Contains(WorkLogType.Break) && generatedWorkday.BreakPeriod is { } breakPeriod)
                {
                    workLogsToCreate.Add(BuildWorkLog(employee.Id, monthClosing.Id, WorkLogType.Break, breakPeriod.Start, breakPeriod.End, now));
                }

                generatedCount++;
            }

            summaries.Add(new EmployeeWorkLogGenerationSummary(employee.Id, employee.Name, generatedCount, skippedCount));
        }

        var unclosedExistingWorkLogs = await _employeeWorkLogRepository.ListUnclosedByDateRangeAsync(
            rangeStart, rangeEnd, cancellationToken);

        foreach (var workLog in unclosedExistingWorkLogs)
        {
            workLog.MonthClosingId = monthClosing.Id;
            workLog.Touch();
        }

        await _monthClosingRepository.AddAsync(monthClosing, cancellationToken);
        await _employeeWorkLogRepository.AddRangeAsync(workLogsToCreate, cancellationToken);
        await _employeeWorkLogRepository.UpdateRangeAsync(unclosedExistingWorkLogs, cancellationToken);

        return new MonthClosingResult(monthClosing, summaries);
    }

    private static EmployeeWorkLog BuildWorkLog(
        Guid employeeId,
        Guid monthClosingId,
        WorkLogType type,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset now)
    {
        var workLog = new EmployeeWorkLog
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            MonthClosingId = monthClosingId,
            Type = type,
            Origin = WorkLogOrigin.Automatic,
            StartDate = start,
            EndDate = end,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        workLog.CalculateDuration();
        return workLog;
    }
}

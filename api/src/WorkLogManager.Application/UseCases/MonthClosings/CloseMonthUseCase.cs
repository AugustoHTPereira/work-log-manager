using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Results;
using WorkLogManager.Application.Services;
using WorkLogManager.Application.UseCases.SystemSettings;

namespace WorkLogManager.Application.UseCases.MonthClosings;

/// <summary>
/// Closes a fully completed past calendar month by automatically generating
/// <see cref="WorkLogType.RegularAttendance"/> (and, when applicable, <see cref="WorkLogType.Break"/>)
/// work logs for every business day and every employee, based on each employee's effective
/// daily work hours.
/// </summary>
public class CloseMonthUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;
    private readonly IMonthClosingRepository _monthClosingRepository;
    private readonly GetSystemSettingsUseCase _getSystemSettingsUseCase;
    private readonly WorkLogGenerationService _workLogGenerationService;
    private readonly IValidator<MonthClosing> _validator;

    public CloseMonthUseCase(
        IEmployeeRepository employeeRepository,
        IEmployeeWorkLogRepository employeeWorkLogRepository,
        IMonthClosingRepository monthClosingRepository,
        GetSystemSettingsUseCase getSystemSettingsUseCase,
        WorkLogGenerationService workLogGenerationService,
        IValidator<MonthClosing> validator)
    {
        _employeeRepository = employeeRepository;
        _employeeWorkLogRepository = employeeWorkLogRepository;
        _monthClosingRepository = monthClosingRepository;
        _getSystemSettingsUseCase = getSystemSettingsUseCase;
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

        var employees = await _employeeRepository.ListAllAsync(cancellationToken);
        var systemSettings = await _getSystemSettingsUseCase.ExecuteAsync(cancellationToken);

        var rangeStart = new DateTimeOffset(input.Year, input.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var rangeEnd = rangeStart.AddMonths(1);

        var existingRegularAttendances = await _employeeWorkLogRepository.ListByTypeAndDateRangeAsync(
            WorkLogType.RegularAttendance, rangeStart, rangeEnd, cancellationToken);

        var existingDays = existingRegularAttendances
            .Select(w => (EmployeeId: w.EmployeeId, Date: DateOnly.FromDateTime(w.StartDate.UtcDateTime)))
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
            var effectiveDailyWorkHours = employee.DailyWorkHours ?? systemSettings.DefaultDailyWorkHours;
            var generatedCount = 0;
            var skippedCount = 0;

            for (var day = 1; day <= daysInMonth; day++)
            {
                var date = new DateOnly(input.Year, input.Month, day);
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    continue;
                }

                if (existingDays.Contains((employee.Id, date)))
                {
                    skippedCount++;
                    continue;
                }

                var generatedWorkday = _workLogGenerationService.GenerateWorkday(date, effectiveDailyWorkHours);

                foreach (var period in generatedWorkday.RegularAttendancePeriods)
                {
                    workLogsToCreate.Add(BuildWorkLog(employee.Id, monthClosing.Id, WorkLogType.RegularAttendance, period.Start, period.End, now));
                }

                if (generatedWorkday.BreakPeriod is { } breakPeriod)
                {
                    workLogsToCreate.Add(BuildWorkLog(employee.Id, monthClosing.Id, WorkLogType.Break, breakPeriod.Start, breakPeriod.End, now));
                }

                generatedCount++;
            }

            summaries.Add(new EmployeeWorkLogGenerationSummary(employee.Id, employee.Name, generatedCount, skippedCount));
        }

        await _monthClosingRepository.AddAsync(monthClosing, cancellationToken);
        await _employeeWorkLogRepository.AddRangeAsync(workLogsToCreate, cancellationToken);

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
            StartDate = start,
            EndDate = end,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        workLog.CalculateDuration();
        return workLog;
    }
}

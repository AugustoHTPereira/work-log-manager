using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Results;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.MonthClosings;

/// <summary>
/// Builds the PDF attendance report for an already-closed month, on demand (see
/// <c>IMonthClosingReportRenderer</c>). Only employees with at least one
/// <see cref="EmployeeWorkLog"/> linked to the given <see cref="MonthClosing"/> appear in the
/// report - employees never enter the grouping otherwise, so there is no explicit
/// "has no work logs" filter needed.
/// </summary>
public class GenerateMonthClosingReportUseCase
{
    private readonly IMonthClosingRepository _monthClosingRepository;
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IMonthClosingReportRenderer _reportRenderer;

    public GenerateMonthClosingReportUseCase(
        IMonthClosingRepository monthClosingRepository,
        IEmployeeWorkLogRepository employeeWorkLogRepository,
        IEmployeeRepository employeeRepository,
        IMonthClosingReportRenderer reportRenderer)
    {
        _monthClosingRepository = monthClosingRepository;
        _employeeWorkLogRepository = employeeWorkLogRepository;
        _employeeRepository = employeeRepository;
        _reportRenderer = reportRenderer;
    }

    public async Task<MonthClosingReportFile> ExecuteAsync(Guid monthClosingId, CancellationToken cancellationToken = default)
    {
        var monthClosing = await _monthClosingRepository.GetByIdAsync(monthClosingId, cancellationToken)
            ?? throw new NotFoundException($"Month closing '{monthClosingId}' was not found.");

        var workLogs = await _employeeWorkLogRepository.ListByMonthClosingIdAsync(monthClosingId, cancellationToken);
        var workLogsByEmployeeId = workLogs.GroupBy(w => w.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

        var employees = await _employeeRepository.ListAllAsync(cancellationToken);
        var employeesById = employees.ToDictionary(e => e.Id);

        var daysInMonth = DateTime.DaysInMonth(monthClosing.Year, monthClosing.Month);

        var sections = new List<EmployeeReportSection>();
        foreach (var (employeeId, employeeWorkLogs) in workLogsByEmployeeId)
        {
            if (!employeesById.TryGetValue(employeeId, out var employee))
            {
                continue;
            }

            var workLogsByDate = employeeWorkLogs
                .GroupBy(w => BusinessTimeZone.ToBusinessDate(w.StartDate))
                .ToDictionary(g => g.Key, g => g.OrderBy(w => w.StartDate).ToList());

            var days = new List<DailyReportRow>();
            for (var day = 1; day <= daysInMonth; day++)
            {
                var date = new DateOnly(monthClosing.Year, monthClosing.Month, day);
                var dayWorkLogs = workLogsByDate.GetValueOrDefault(date, []);

                var entries = dayWorkLogs
                    .Select(w => new WorkLogReportEntry(w.StartDate, w.EndDate, w.Type))
                    .ToList();

                var totalSeconds = dayWorkLogs.Sum(w => w.DurationSeconds);

                var observationText = string.Join(
                    "; ",
                    dayWorkLogs
                        .Select(w => w.Note)
                        .Where(note => !string.IsNullOrWhiteSpace(note)));

                days.Add(new DailyReportRow(
                    date,
                    entries,
                    totalSeconds,
                    string.IsNullOrEmpty(observationText) ? null : observationText));
            }

            sections.Add(new EmployeeReportSection(employee.Id, employee.Name, employee.Role, days));
        }

        var orderedSections = sections.OrderBy(s => s.EmployeeName, StringComparer.Ordinal).ToList();
        var reportData = new MonthClosingReportData(monthClosing, orderedSections);

        var content = _reportRenderer.Render(reportData);
        var fileName = $"fechamento-{monthClosing.Month:00}-{monthClosing.Year:0000}.pdf";

        return new MonthClosingReportFile(content, fileName);
    }
}

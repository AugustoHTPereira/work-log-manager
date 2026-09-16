using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Results;

/// <summary>
/// Aggregate data for the month closing PDF report built by
/// <c>GenerateMonthClosingReportUseCase</c> and rendered by
/// <c>IMonthClosingReportRenderer</c>. Not a persisted entity - the PDF is generated on
/// demand and never stored (see the approved development plan for this task).
/// </summary>
public record MonthClosingReportData(MonthClosing MonthClosing, IReadOnlyList<EmployeeReportSection> Employees);

/// <summary>
/// One employee's section of the report: one PDF page (or more, if the table overflows a
/// single page - see <c>MonthClosingReportRenderer</c>) per <see cref="EmployeeReportSection"/>.
/// </summary>
public record EmployeeReportSection(
    Guid EmployeeId,
    string EmployeeName,
    string EmployeeRole,
    IReadOnlyList<DailyReportRow> Days);

/// <summary>
/// A single calendar day row in an employee's report table. <see cref="Entries"/> is empty
/// for days with no <see cref="EmployeeWorkLog"/> (rendered as a blank row).
/// </summary>
public record DailyReportRow(
    DateOnly Date,
    IReadOnlyList<WorkLogReportEntry> Entries,
    long TotalSeconds,
    string? ObservationText);

/// <summary>
/// A single <see cref="EmployeeWorkLog"/> as displayed in the report's "Horários" cell.
/// </summary>
public record WorkLogReportEntry(DateTimeOffset Start, DateTimeOffset End, WorkLogType Type);

/// <summary>
/// Final output of <c>GenerateMonthClosingReportUseCase</c>: the rendered PDF bytes plus a
/// suggested file name.
/// </summary>
public record MonthClosingReportFile(byte[] Content, string FileName);

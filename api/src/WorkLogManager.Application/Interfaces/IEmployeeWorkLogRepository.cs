using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Interfaces;

public interface IEmployeeWorkLogRepository
{
    Task AddAsync(EmployeeWorkLog workLog, CancellationToken cancellationToken = default);
    Task UpdateAsync(EmployeeWorkLog workLog, CancellationToken cancellationToken = default);
    Task DeleteAsync(EmployeeWorkLog workLog, CancellationToken cancellationToken = default);
    Task<EmployeeWorkLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the work logs of a single employee, optionally narrowed down by a
    /// <see cref="EmployeeWorkLog.StartDate"/> range and/or <see cref="WorkLogType"/>/
    /// <see cref="WorkLogOrigin"/>. Every filter parameter is optional (<c>null</c> means "no
    /// restriction on this dimension"), backing the <c>GET /employees/{id}/work-logs</c>
    /// endpoint's query-string filters.
    /// </summary>
    Task<IReadOnlyList<EmployeeWorkLog>> ListByEmployeeIdAsync(
        Guid employeeId,
        DateTimeOffset? startDateInclusive = null,
        DateTimeOffset? endDateInclusive = null,
        WorkLogType? type = null,
        WorkLogOrigin? origin = null,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<EmployeeWorkLog> workLogs, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeWorkLog>> ListByTypeAndDateRangeAsync(
        WorkLogType type,
        DateTimeOffset rangeStartInclusive,
        DateTimeOffset rangeEndExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every <see cref="EmployeeWorkLog"/> (any <see cref="WorkLogType"/>) whose
    /// <see cref="EmployeeWorkLog.StartDate"/> falls within the given range and that is not
    /// yet linked to a <see cref="MonthClosing"/> (<see cref="EmployeeWorkLog.MonthClosingId"/>
    /// is <c>null</c>). Used by the "close month" flow to associate pre-existing manual work
    /// logs with the closing being created.
    /// </summary>
    Task<IReadOnlyList<EmployeeWorkLog>> ListUnclosedByDateRangeAsync(
        DateTimeOffset rangeStartInclusive,
        DateTimeOffset rangeEndExclusive,
        CancellationToken cancellationToken = default);

    Task UpdateRangeAsync(IEnumerable<EmployeeWorkLog> workLogs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every <see cref="EmployeeWorkLog"/> linked to the given <see cref="MonthClosing"/>
    /// (<see cref="EmployeeWorkLog.MonthClosingId"/> equal to <paramref name="monthClosingId"/>),
    /// used by <c>GenerateMonthClosingReportUseCase</c> to build the closing's PDF report.
    /// </summary>
    Task<IReadOnlyList<EmployeeWorkLog>> ListByMonthClosingIdAsync(
        Guid monthClosingId,
        CancellationToken cancellationToken = default);

    Task DeleteRangeAsync(IEnumerable<EmployeeWorkLog> workLogs, CancellationToken cancellationToken = default);
}

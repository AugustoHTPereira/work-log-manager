using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Interfaces;

public interface IEmployeeWorkLogRepository
{
    Task AddAsync(EmployeeWorkLog workLog, CancellationToken cancellationToken = default);
    Task UpdateAsync(EmployeeWorkLog workLog, CancellationToken cancellationToken = default);
    Task DeleteAsync(EmployeeWorkLog workLog, CancellationToken cancellationToken = default);
    Task<EmployeeWorkLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeWorkLog>> ListByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<EmployeeWorkLog> workLogs, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeWorkLog>> ListByTypeAndDateRangeAsync(
        WorkLogType type,
        DateTimeOffset rangeStartInclusive,
        DateTimeOffset rangeEndExclusive,
        CancellationToken cancellationToken = default);
}

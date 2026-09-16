using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Interfaces;

public interface IMonthClosingRepository
{
    Task<MonthClosing?> GetByMonthYearAsync(int month, int year, CancellationToken cancellationToken = default);
    Task<MonthClosing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(MonthClosing monthClosing, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthClosing>> ListAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(MonthClosing monthClosing, CancellationToken cancellationToken = default);
}

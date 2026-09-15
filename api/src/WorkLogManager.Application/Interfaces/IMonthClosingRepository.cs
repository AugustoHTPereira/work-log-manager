using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Interfaces;

public interface IMonthClosingRepository
{
    Task<MonthClosing?> GetByMonthYearAsync(int month, int year, CancellationToken cancellationToken = default);
    Task AddAsync(MonthClosing monthClosing, CancellationToken cancellationToken = default);
}

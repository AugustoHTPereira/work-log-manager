using Microsoft.EntityFrameworkCore;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Infrastructure.Persistence.Repositories;

public class MonthClosingRepository : IMonthClosingRepository
{
    private readonly WorkLogManagerDbContext _dbContext;

    public MonthClosingRepository(WorkLogManagerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<MonthClosing?> GetByMonthYearAsync(int month, int year, CancellationToken cancellationToken = default)
    {
        return _dbContext.MonthClosings.FirstOrDefaultAsync(m => m.Month == month && m.Year == year, cancellationToken);
    }

    public Task<MonthClosing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.MonthClosings.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task AddAsync(MonthClosing monthClosing, CancellationToken cancellationToken = default)
    {
        await _dbContext.MonthClosings.AddAsync(monthClosing, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonthClosing>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.MonthClosings
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(MonthClosing monthClosing, CancellationToken cancellationToken = default)
    {
        _dbContext.MonthClosings.Remove(monthClosing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

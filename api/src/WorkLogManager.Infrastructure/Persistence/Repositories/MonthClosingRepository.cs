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

    public async Task AddAsync(MonthClosing monthClosing, CancellationToken cancellationToken = default)
    {
        await _dbContext.MonthClosings.AddAsync(monthClosing, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

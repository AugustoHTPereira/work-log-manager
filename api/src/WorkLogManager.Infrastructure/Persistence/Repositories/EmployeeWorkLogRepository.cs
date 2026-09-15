using Microsoft.EntityFrameworkCore;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Infrastructure.Persistence.Repositories;

public class EmployeeWorkLogRepository : IEmployeeWorkLogRepository
{
    private readonly WorkLogManagerDbContext _dbContext;

    public EmployeeWorkLogRepository(WorkLogManagerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(EmployeeWorkLog workLog, CancellationToken cancellationToken = default)
    {
        await _dbContext.EmployeeWorkLogs.AddAsync(workLog, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(EmployeeWorkLog workLog, CancellationToken cancellationToken = default)
    {
        _dbContext.EmployeeWorkLogs.Update(workLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(EmployeeWorkLog workLog, CancellationToken cancellationToken = default)
    {
        _dbContext.EmployeeWorkLogs.Remove(workLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<EmployeeWorkLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.EmployeeWorkLogs.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeWorkLog>> ListByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeWorkLogs
            .Where(w => w.EmployeeId == employeeId)
            .OrderByDescending(w => w.StartDate)
            .ToListAsync(cancellationToken);
    }
}

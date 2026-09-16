using Microsoft.EntityFrameworkCore;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Infrastructure.Persistence.Repositories;

public class WorkSchedulePeriodRepository : IWorkSchedulePeriodRepository
{
    private readonly WorkLogManagerDbContext _dbContext;

    public WorkSchedulePeriodRepository(WorkLogManagerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WorkSchedulePeriod>> ListAsync(Guid? employeeId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkSchedulePeriods
            .Where(p => p.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkSchedulePeriod>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkSchedulePeriods.ToListAsync(cancellationToken);
    }

    public async Task ReplaceAsync(Guid? employeeId, IReadOnlyList<WorkSchedulePeriod> periods, CancellationToken cancellationToken = default)
    {
        var existingPeriods = await _dbContext.WorkSchedulePeriods
            .Where(p => p.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        _dbContext.WorkSchedulePeriods.RemoveRange(existingPeriods);
        await _dbContext.WorkSchedulePeriods.AddRangeAsync(periods, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

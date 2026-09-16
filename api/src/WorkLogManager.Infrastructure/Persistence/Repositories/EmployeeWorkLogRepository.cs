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

    public async Task<IReadOnlyList<EmployeeWorkLog>> ListByEmployeeIdAsync(
        Guid employeeId,
        DateTimeOffset? startDateInclusive = null,
        DateTimeOffset? endDateInclusive = null,
        WorkLogType? type = null,
        WorkLogOrigin? origin = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.EmployeeWorkLogs.Where(w => w.EmployeeId == employeeId);

        if (startDateInclusive is not null)
        {
            query = query.Where(w => w.StartDate >= startDateInclusive);
        }

        if (endDateInclusive is not null)
        {
            query = query.Where(w => w.StartDate <= endDateInclusive);
        }

        if (type is not null)
        {
            query = query.Where(w => w.Type == type);
        }

        if (origin is not null)
        {
            query = query.Where(w => w.Origin == origin);
        }

        return await query
            .OrderByDescending(w => w.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<EmployeeWorkLog> workLogs, CancellationToken cancellationToken = default)
    {
        await _dbContext.EmployeeWorkLogs.AddRangeAsync(workLogs, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeWorkLog>> ListByTypeAndDateRangeAsync(
        WorkLogType type,
        DateTimeOffset rangeStartInclusive,
        DateTimeOffset rangeEndExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeWorkLogs
            .Where(w => w.Type == type && w.StartDate >= rangeStartInclusive && w.StartDate < rangeEndExclusive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeWorkLog>> ListUnclosedByDateRangeAsync(
        DateTimeOffset rangeStartInclusive,
        DateTimeOffset rangeEndExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeWorkLogs
            .Where(w => w.MonthClosingId == null && w.StartDate >= rangeStartInclusive && w.StartDate < rangeEndExclusive)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<EmployeeWorkLog> workLogs, CancellationToken cancellationToken = default)
    {
        _dbContext.EmployeeWorkLogs.UpdateRange(workLogs);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeWorkLog>> ListByMonthClosingIdAsync(
        Guid monthClosingId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmployeeWorkLogs
            .Where(w => w.MonthClosingId == monthClosingId)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteRangeAsync(IEnumerable<EmployeeWorkLog> workLogs, CancellationToken cancellationToken = default)
    {
        _dbContext.EmployeeWorkLogs.RemoveRange(workLogs);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

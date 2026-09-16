using Microsoft.EntityFrameworkCore;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Infrastructure.Persistence.Repositories;

public class SystemParameterRepository : ISystemParameterRepository
{
    private readonly WorkLogManagerDbContext _dbContext;

    public SystemParameterRepository(WorkLogManagerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SystemParameter>> ListAsync(
        IReadOnlyList<SystemParameterName>? paramFilter = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SystemParameters.AsQueryable();

        if (paramFilter is { Count: > 0 })
        {
            query = query.Where(p => paramFilter.Contains(p.Param));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SystemParameter>> ListByParamAsync(
        SystemParameterName param, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SystemParameters
            .Where(p => p.Param == param)
            .ToListAsync(cancellationToken);
    }

    public Task<SystemParameter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.SystemParameters.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task AddAsync(SystemParameter parameter, CancellationToken cancellationToken = default)
    {
        await _dbContext.SystemParameters.AddAsync(parameter, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SystemParameter parameter, CancellationToken cancellationToken = default)
    {
        _dbContext.SystemParameters.Update(parameter);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(SystemParameter parameter, CancellationToken cancellationToken = default)
    {
        _dbContext.SystemParameters.Remove(parameter);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

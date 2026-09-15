using Microsoft.EntityFrameworkCore;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Infrastructure.Persistence.Repositories;

public class SystemSettingsRepository : ISystemSettingsRepository
{
    private readonly WorkLogManagerDbContext _dbContext;

    public SystemSettingsRepository(WorkLogManagerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Application.Entities.SystemSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(Application.Entities.SystemSettings systemSettings, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.SystemSettings.AnyAsync(s => s.Id == systemSettings.Id, cancellationToken);
        if (exists)
        {
            _dbContext.SystemSettings.Update(systemSettings);
        }
        else
        {
            await _dbContext.SystemSettings.AddAsync(systemSettings, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

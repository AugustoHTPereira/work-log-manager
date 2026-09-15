using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Interfaces;

public interface ISystemSettingsRepository
{
    Task<SystemSettings?> GetAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(SystemSettings systemSettings, CancellationToken cancellationToken = default);
}

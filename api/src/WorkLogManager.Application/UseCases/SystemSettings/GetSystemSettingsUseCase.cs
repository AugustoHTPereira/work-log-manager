using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.SystemSettings;

public class GetSystemSettingsUseCase
{
    private const decimal DefaultDailyWorkHoursFallback = 8m;

    private readonly ISystemSettingsRepository _systemSettingsRepository;

    public GetSystemSettingsUseCase(ISystemSettingsRepository systemSettingsRepository)
    {
        _systemSettingsRepository = systemSettingsRepository;
    }

    public async Task<Entities.SystemSettings> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var systemSettings = await _systemSettingsRepository.GetAsync(cancellationToken);
        if (systemSettings is not null)
        {
            return systemSettings;
        }

        var now = DateTimeOffset.UtcNow;
        var created = new Entities.SystemSettings
        {
            DefaultDailyWorkHours = DefaultDailyWorkHoursFallback,
            UpdatedAtUtc = now,
        };

        await _systemSettingsRepository.UpsertAsync(created, cancellationToken);

        return created;
    }
}

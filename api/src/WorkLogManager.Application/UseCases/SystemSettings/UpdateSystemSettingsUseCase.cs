using FluentValidation;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.SystemSettings;

public class UpdateSystemSettingsUseCase
{
    private readonly ISystemSettingsRepository _systemSettingsRepository;
    private readonly GetSystemSettingsUseCase _getSystemSettingsUseCase;
    private readonly IValidator<Entities.SystemSettings> _validator;

    public UpdateSystemSettingsUseCase(
        ISystemSettingsRepository systemSettingsRepository,
        GetSystemSettingsUseCase getSystemSettingsUseCase,
        IValidator<Entities.SystemSettings> validator)
    {
        _systemSettingsRepository = systemSettingsRepository;
        _getSystemSettingsUseCase = getSystemSettingsUseCase;
        _validator = validator;
    }

    public async Task<Entities.SystemSettings> ExecuteAsync(Entities.SystemSettings systemSettings, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(systemSettings, cancellationToken);

        var existingSystemSettings = await _getSystemSettingsUseCase.ExecuteAsync(cancellationToken);

        existingSystemSettings.DefaultDailyWorkHours = systemSettings.DefaultDailyWorkHours;
        existingSystemSettings.Touch();

        await _systemSettingsRepository.UpsertAsync(existingSystemSettings, cancellationToken);

        return existingSystemSettings;
    }
}

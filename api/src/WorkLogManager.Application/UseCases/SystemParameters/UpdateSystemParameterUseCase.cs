using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.SystemParameters;

/// <summary>
/// Upserts the single row of a scalar system parameter (currently only
/// <see cref="SystemParameterValueType.Bool"/> ones). Array parameters (e.g.
/// <see cref="SystemParameterName.AutoWorkLogTypes"/>) are managed one row at a time by
/// <see cref="AddSystemParameterValueUseCase"/>/<see cref="RemoveSystemParameterValueUseCase"/>
/// instead.
/// </summary>
public class UpdateSystemParameterUseCase
{
    private readonly ISystemParameterRepository _repository;
    private readonly IValidator<SystemParameter> _validator;

    public UpdateSystemParameterUseCase(ISystemParameterRepository repository, IValidator<SystemParameter> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<SystemParameter> ExecuteAsync(
        SystemParameterName param, string value, CancellationToken cancellationToken = default)
    {
        var valueType = SystemParameterCatalog.GetValueType(param);
        if (valueType != SystemParameterValueType.Bool)
        {
            throw new DomainException(
                $"System parameter '{param}' has value type '{valueType}' and cannot be updated via PUT; use POST/DELETE instead.");
        }

        var existingRows = await _repository.ListByParamAsync(param, cancellationToken);
        var existing = existingRows.FirstOrDefault();
        var now = DateTimeOffset.UtcNow;

        var candidate = new SystemParameter
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            Param = param,
            Value = value,
            ValueType = valueType,
            CreatedAtUtc = existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc = now,
        };

        await _validator.ValidateAndThrowAsync(candidate, cancellationToken);

        if (existing is null)
        {
            await _repository.AddAsync(candidate, cancellationToken);
        }
        else
        {
            await _repository.UpdateAsync(candidate, cancellationToken);
        }

        return candidate;
    }
}

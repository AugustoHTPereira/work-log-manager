using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.SystemParameters;

/// <summary>
/// Adds one row to an array system parameter (currently only
/// <see cref="SystemParameterName.AutoWorkLogTypes"/>), e.g. enabling a checkbox. Idempotent:
/// if a row with the same <c>(Param, Value)</c> already exists, it is returned as-is instead of
/// inserting a duplicate (the unique index on <c>(Param, Value)</c> would reject it anyway).
/// </summary>
public class AddSystemParameterValueUseCase
{
    private readonly ISystemParameterRepository _repository;
    private readonly IValidator<SystemParameter> _validator;

    public AddSystemParameterValueUseCase(ISystemParameterRepository repository, IValidator<SystemParameter> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<SystemParameter> ExecuteAsync(
        SystemParameterName param, string value, CancellationToken cancellationToken = default)
    {
        var valueType = SystemParameterCatalog.GetValueType(param);
        if (valueType != SystemParameterValueType.Array)
        {
            throw new DomainException(
                $"System parameter '{param}' has value type '{valueType}' and does not support adding individual values; use PUT instead.");
        }

        var existingRows = await _repository.ListByParamAsync(param, cancellationToken);
        var existing = existingRows.FirstOrDefault(row => row.Value == value);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var candidate = new SystemParameter
        {
            Id = Guid.NewGuid(),
            Param = param,
            Value = value,
            ValueType = valueType,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await _validator.ValidateAndThrowAsync(candidate, cancellationToken);

        await _repository.AddAsync(candidate, cancellationToken);

        return candidate;
    }
}

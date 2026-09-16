using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.SystemParameters;

/// <summary>
/// Removes one row of an array system parameter (currently only
/// <see cref="SystemParameterName.AutoWorkLogTypes"/>), e.g. disabling a checkbox. The row is
/// only deleted when both <paramref name="id"/> and <paramref name="param"/> match the stored
/// row, so that an <paramref name="id"/> belonging to a different parameter can never be used to
/// delete the wrong row.
/// </summary>
public class RemoveSystemParameterValueUseCase
{
    private readonly ISystemParameterRepository _repository;

    public RemoveSystemParameterValueUseCase(ISystemParameterRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(SystemParameterName param, Guid id, CancellationToken cancellationToken = default)
    {
        var valueType = SystemParameterCatalog.GetValueType(param);
        if (valueType != SystemParameterValueType.Array)
        {
            throw new DomainException(
                $"System parameter '{param}' has value type '{valueType}' and does not support removing individual values.");
        }

        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null || existing.Param != param)
        {
            throw new NotFoundException($"System parameter value '{id}' was not found for parameter '{param}'.");
        }

        await _repository.DeleteAsync(existing, cancellationToken);
    }
}

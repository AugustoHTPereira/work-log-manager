using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.SystemParameters;

/// <summary>
/// Lists every row stored for a given <see cref="SystemParameterName"/>. Scalar parameters
/// (e.g. <see cref="SystemParameterValueType.Bool"/>) return zero or one row; array parameters
/// (e.g. <see cref="SystemParameterValueType.Array"/>) may return several. An empty result is a
/// valid "not configured" state and is returned as an empty list rather than a 404, since this
/// use case models a collection, not a single resource lookup.
/// </summary>
public class ListSystemParameterValuesByNameUseCase
{
    private readonly ISystemParameterRepository _repository;

    public ListSystemParameterValuesByNameUseCase(ISystemParameterRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<SystemParameter>> ExecuteAsync(SystemParameterName param, CancellationToken cancellationToken = default)
    {
        return _repository.ListByParamAsync(param, cancellationToken);
    }
}

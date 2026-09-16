using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.SystemParameters;

public class ListSystemParametersUseCase
{
    private readonly ISystemParameterRepository _repository;

    public ListSystemParametersUseCase(ISystemParameterRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<SystemParameter>> ExecuteAsync(
        IReadOnlyList<SystemParameterName>? paramFilter = null, CancellationToken cancellationToken = default)
    {
        return _repository.ListAsync(paramFilter, cancellationToken);
    }
}

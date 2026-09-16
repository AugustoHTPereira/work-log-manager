using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Interfaces;

public interface ISystemParameterRepository
{
    Task<IReadOnlyList<SystemParameter>> ListAsync(
        IReadOnlyList<SystemParameterName>? paramFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every row currently stored for <paramref name="param"/>. Scalar parameters
    /// (e.g. <see cref="SystemParameterValueType.Bool"/>) have at most one row; array
    /// parameters (e.g. <see cref="SystemParameterValueType.Array"/>) may have zero or more.
    /// </summary>
    Task<IReadOnlyList<SystemParameter>> ListByParamAsync(SystemParameterName param, CancellationToken cancellationToken = default);

    Task<SystemParameter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(SystemParameter parameter, CancellationToken cancellationToken = default);

    Task UpdateAsync(SystemParameter parameter, CancellationToken cancellationToken = default);

    Task DeleteAsync(SystemParameter parameter, CancellationToken cancellationToken = default);
}

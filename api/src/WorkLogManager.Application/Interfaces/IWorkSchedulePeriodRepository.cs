using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Interfaces;

public interface IWorkSchedulePeriodRepository
{
    /// <summary>
    /// Lists the periods belonging to a single owner: the general configuration when
    /// <paramref name="employeeId"/> is <c>null</c>, or that employee's configuration otherwise.
    /// </summary>
    Task<IReadOnlyList<WorkSchedulePeriod>> ListAsync(Guid? employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every period (general and all employees). Used only by
    /// <c>CloseMonthUseCase</c>, which needs the full set to resolve every employee's
    /// effective schedule without N+1 queries.
    /// </summary>
    Task<IReadOnlyList<WorkSchedulePeriod>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces every period belonging to a single owner (general when
    /// <paramref name="employeeId"/> is <c>null</c>, or that employee otherwise) with
    /// <paramref name="periods"/>.
    /// </summary>
    Task ReplaceAsync(Guid? employeeId, IReadOnlyList<WorkSchedulePeriod> periods, CancellationToken cancellationToken = default);
}

using FluentValidation;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.WorkSchedules;

/// <summary>
/// Replaces the whole general (company-wide) work schedule with <paramref name="periods"/>
/// (each item is validated, and the full set is checked for same-day overlaps before persisting).
/// </summary>
public class UpdateGeneralWorkScheduleUseCase
{
    private readonly IWorkSchedulePeriodRepository _workSchedulePeriodRepository;
    private readonly IValidator<WorkSchedulePeriod> _validator;

    public UpdateGeneralWorkScheduleUseCase(
        IWorkSchedulePeriodRepository workSchedulePeriodRepository,
        IValidator<WorkSchedulePeriod> validator)
    {
        _workSchedulePeriodRepository = workSchedulePeriodRepository;
        _validator = validator;
    }

    public async Task<IReadOnlyList<WorkSchedulePeriod>> ExecuteAsync(
        IReadOnlyList<WorkSchedulePeriod> periods,
        CancellationToken cancellationToken = default)
    {
        foreach (var period in periods)
        {
            await _validator.ValidateAndThrowAsync(period, cancellationToken);
        }

        WorkScheduleOverlapChecker.EnsureNoOverlap(periods);

        var now = DateTimeOffset.UtcNow;
        foreach (var period in periods)
        {
            period.Id = Guid.NewGuid();
            period.EmployeeId = null;
            period.CreatedAtUtc = now;
            period.UpdatedAtUtc = now;
        }

        await _workSchedulePeriodRepository.ReplaceAsync(null, periods, cancellationToken);

        return periods;
    }
}

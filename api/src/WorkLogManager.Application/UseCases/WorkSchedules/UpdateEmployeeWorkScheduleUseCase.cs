using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.WorkSchedules;

/// <summary>
/// Replaces the whole work schedule of a single employee with <paramref name="periods"/>
/// (each item is validated, and the full set is checked for same-day overlaps before persisting).
/// </summary>
public class UpdateEmployeeWorkScheduleUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IWorkSchedulePeriodRepository _workSchedulePeriodRepository;
    private readonly IValidator<WorkSchedulePeriod> _validator;

    public UpdateEmployeeWorkScheduleUseCase(
        IEmployeeRepository employeeRepository,
        IWorkSchedulePeriodRepository workSchedulePeriodRepository,
        IValidator<WorkSchedulePeriod> validator)
    {
        _employeeRepository = employeeRepository;
        _workSchedulePeriodRepository = workSchedulePeriodRepository;
        _validator = validator;
    }

    public async Task<IReadOnlyList<WorkSchedulePeriod>> ExecuteAsync(
        Guid employeeId,
        IReadOnlyList<WorkSchedulePeriod> periods,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' was not found.");

        foreach (var period in periods)
        {
            await _validator.ValidateAndThrowAsync(period, cancellationToken);
        }

        WorkScheduleOverlapChecker.EnsureNoOverlap(periods);

        var now = DateTimeOffset.UtcNow;
        foreach (var period in periods)
        {
            period.Id = Guid.NewGuid();
            period.EmployeeId = employee.Id;
            period.CreatedAtUtc = now;
            period.UpdatedAtUtc = now;
        }

        await _workSchedulePeriodRepository.ReplaceAsync(employee.Id, periods, cancellationToken);

        return periods;
    }
}

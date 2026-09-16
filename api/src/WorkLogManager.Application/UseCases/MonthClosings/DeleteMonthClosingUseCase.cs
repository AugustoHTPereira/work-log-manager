using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Results;

namespace WorkLogManager.Application.UseCases.MonthClosings;

/// <summary>
/// Deletes a <see cref="MonthClosing"/>, always ignoring the "closed work logs cannot be
/// managed" restriction (unlike <see cref="EmployeeWorkLogs.UpdateEmployeeWorkLogUseCase"/>/
/// <see cref="EmployeeWorkLogs.DeleteEmployeeWorkLogUseCase"/>, this use-case never consults
/// <see cref="ISystemParameterRepository"/>/<c>AllowManageClosedWorkLogsParser</c> - deleting the
/// closing itself is always allowed). Every linked <see cref="EmployeeWorkLog"/> with
/// <see cref="WorkLogOrigin.Automatic"/> is deleted, while every linked
/// <see cref="WorkLogOrigin.Manual"/> one is only unlinked (<see cref="EmployeeWorkLog.MonthClosingId"/>
/// set back to <c>null</c>), never deleted. Operations run in this order - delete automatic work
/// logs, then unlink manual ones, then delete the closing itself - so that, in the absence of an
/// explicit transaction, a partial failure never leaves the FK-restricted relationship broken and
/// can always be safely retried (see "Sobre atomicidade" in the approved plan).
/// </summary>
public class DeleteMonthClosingUseCase
{
    private readonly IMonthClosingRepository _monthClosingRepository;
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;

    public DeleteMonthClosingUseCase(
        IMonthClosingRepository monthClosingRepository,
        IEmployeeWorkLogRepository employeeWorkLogRepository)
    {
        _monthClosingRepository = monthClosingRepository;
        _employeeWorkLogRepository = employeeWorkLogRepository;
    }

    public async Task<MonthClosingDeletionResult> ExecuteAsync(Guid monthClosingId, CancellationToken cancellationToken = default)
    {
        var monthClosing = await _monthClosingRepository.GetByIdAsync(monthClosingId, cancellationToken);
        if (monthClosing is null)
        {
            throw new NotFoundException($"Month closing '{monthClosingId}' was not found.");
        }

        var linkedWorkLogs = await _employeeWorkLogRepository.ListByMonthClosingIdAsync(monthClosingId, cancellationToken);

        var automaticWorkLogs = linkedWorkLogs.Where(w => w.Origin == WorkLogOrigin.Automatic).ToList();
        var manualWorkLogs = linkedWorkLogs.Where(w => w.Origin == WorkLogOrigin.Manual).ToList();

        foreach (var workLog in manualWorkLogs)
        {
            workLog.MonthClosingId = null;
            workLog.Touch();
        }

        await _employeeWorkLogRepository.DeleteRangeAsync(automaticWorkLogs, cancellationToken);
        await _employeeWorkLogRepository.UpdateRangeAsync(manualWorkLogs, cancellationToken);
        await _monthClosingRepository.DeleteAsync(monthClosing, cancellationToken);

        return new MonthClosingDeletionResult(
            monthClosing.Id,
            monthClosing.Month,
            monthClosing.Year,
            automaticWorkLogs.Count,
            manualWorkLogs.Count);
    }
}

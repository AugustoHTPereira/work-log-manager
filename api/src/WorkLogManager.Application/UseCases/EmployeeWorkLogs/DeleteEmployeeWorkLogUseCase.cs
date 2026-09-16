using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.EmployeeWorkLogs;

public class DeleteEmployeeWorkLogUseCase
{
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;
    private readonly ISystemParameterRepository _systemParameterRepository;

    public DeleteEmployeeWorkLogUseCase(
        IEmployeeWorkLogRepository employeeWorkLogRepository, ISystemParameterRepository systemParameterRepository)
    {
        _employeeWorkLogRepository = employeeWorkLogRepository;
        _systemParameterRepository = systemParameterRepository;
    }

    public async Task ExecuteAsync(Guid employeeId, Guid workLogId, CancellationToken cancellationToken = default)
    {
        var workLog = await _employeeWorkLogRepository.GetByIdAsync(workLogId, cancellationToken);
        if (workLog is null || workLog.EmployeeId != employeeId)
        {
            throw new NotFoundException($"Work log '{workLogId}' was not found for employee '{employeeId}'.");
        }

        if (workLog.MonthClosingId is not null)
        {
            var allowManageClosedWorkLogsRows = await _systemParameterRepository.ListByParamAsync(
                SystemParameterName.AllowManageClosedWorkLogs, cancellationToken);
            var allowManageClosedWorkLogs = AllowManageClosedWorkLogsParser.Parse(allowManageClosedWorkLogsRows.FirstOrDefault()?.Value);

            if (!allowManageClosedWorkLogs)
            {
                throw new DomainException("This work log is linked to a month closing and can no longer be deleted.");
            }
        }

        await _employeeWorkLogRepository.DeleteAsync(workLog, cancellationToken);
    }
}

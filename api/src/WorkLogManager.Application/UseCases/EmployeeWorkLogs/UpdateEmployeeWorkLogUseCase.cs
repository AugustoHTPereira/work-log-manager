using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.EmployeeWorkLogs;

public class UpdateEmployeeWorkLogUseCase
{
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;
    private readonly IMonthClosingRepository _monthClosingRepository;
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly IValidator<EmployeeWorkLog> _validator;

    public UpdateEmployeeWorkLogUseCase(
        IEmployeeWorkLogRepository employeeWorkLogRepository,
        IMonthClosingRepository monthClosingRepository,
        ISystemParameterRepository systemParameterRepository,
        IValidator<EmployeeWorkLog> validator)
    {
        _employeeWorkLogRepository = employeeWorkLogRepository;
        _monthClosingRepository = monthClosingRepository;
        _systemParameterRepository = systemParameterRepository;
        _validator = validator;
    }

    public async Task<EmployeeWorkLog> ExecuteAsync(
        Guid employeeId,
        Guid workLogId,
        EmployeeWorkLog workLog,
        CancellationToken cancellationToken = default)
    {
        workLog.EmployeeId = employeeId;
        await _validator.ValidateAndThrowAsync(workLog, cancellationToken);

        var existingWorkLog = await _employeeWorkLogRepository.GetByIdAsync(workLogId, cancellationToken);
        if (existingWorkLog is null || existingWorkLog.EmployeeId != employeeId)
        {
            throw new NotFoundException($"Work log '{workLogId}' was not found for employee '{employeeId}'.");
        }

        var allowManageClosedWorkLogsRows = await _systemParameterRepository.ListByParamAsync(
            SystemParameterName.AllowManageClosedWorkLogs, cancellationToken);
        var allowManageClosedWorkLogs = AllowManageClosedWorkLogsParser.Parse(allowManageClosedWorkLogsRows.FirstOrDefault()?.Value);

        if (existingWorkLog.MonthClosingId is not null && !allowManageClosedWorkLogs)
        {
            throw new DomainException("This work log is linked to a month closing and can no longer be edited.");
        }

        var businessDate = BusinessTimeZone.ToBusinessDate(workLog.StartDate);
        var monthClosing = await _monthClosingRepository.GetByMonthYearAsync(businessDate.Month, businessDate.Year, cancellationToken);
        if (monthClosing is not null && !allowManageClosedWorkLogs)
        {
            throw new DomainException(
                $"Month {businessDate.Month:00}/{businessDate.Year} is already closed; work logs in that month cannot be created or edited.");
        }

        existingWorkLog.Type = workLog.Type;
        existingWorkLog.StartDate = workLog.StartDate;
        existingWorkLog.EndDate = workLog.EndDate;
        existingWorkLog.Note = workLog.Note;
        existingWorkLog.CalculateDuration();
        existingWorkLog.Touch();

        await _employeeWorkLogRepository.UpdateAsync(existingWorkLog, cancellationToken);

        return existingWorkLog;
    }
}

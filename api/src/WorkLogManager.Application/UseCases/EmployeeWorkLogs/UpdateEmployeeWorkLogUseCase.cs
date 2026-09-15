using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.EmployeeWorkLogs;

public class UpdateEmployeeWorkLogUseCase
{
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;
    private readonly IValidator<EmployeeWorkLog> _validator;

    public UpdateEmployeeWorkLogUseCase(IEmployeeWorkLogRepository employeeWorkLogRepository, IValidator<EmployeeWorkLog> validator)
    {
        _employeeWorkLogRepository = employeeWorkLogRepository;
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

        existingWorkLog.Type = workLog.Type;
        existingWorkLog.StartDate = workLog.StartDate;
        existingWorkLog.EndDate = workLog.EndDate;
        existingWorkLog.CalculateDuration();
        existingWorkLog.Touch();

        await _employeeWorkLogRepository.UpdateAsync(existingWorkLog, cancellationToken);

        return existingWorkLog;
    }
}

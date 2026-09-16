using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.UseCases.EmployeeWorkLogs;

public class CreateEmployeeWorkLogUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;
    private readonly IMonthClosingRepository _monthClosingRepository;
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly IValidator<EmployeeWorkLog> _validator;

    public CreateEmployeeWorkLogUseCase(
        IEmployeeRepository employeeRepository,
        IEmployeeWorkLogRepository employeeWorkLogRepository,
        IMonthClosingRepository monthClosingRepository,
        ISystemParameterRepository systemParameterRepository,
        IValidator<EmployeeWorkLog> validator)
    {
        _employeeRepository = employeeRepository;
        _employeeWorkLogRepository = employeeWorkLogRepository;
        _monthClosingRepository = monthClosingRepository;
        _systemParameterRepository = systemParameterRepository;
        _validator = validator;
    }

    public async Task<EmployeeWorkLog> ExecuteAsync(Guid employeeId, EmployeeWorkLog workLog, CancellationToken cancellationToken = default)
    {
        workLog.EmployeeId = employeeId;
        await _validator.ValidateAndThrowAsync(workLog, cancellationToken);

        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' was not found.");

        var businessDate = BusinessTimeZone.ToBusinessDate(workLog.StartDate);
        var monthClosing = await _monthClosingRepository.GetByMonthYearAsync(businessDate.Month, businessDate.Year, cancellationToken);
        if (monthClosing is not null)
        {
            var allowManageClosedWorkLogsRows = await _systemParameterRepository.ListByParamAsync(
                SystemParameterName.AllowManageClosedWorkLogs, cancellationToken);
            var allowManageClosedWorkLogs = AllowManageClosedWorkLogsParser.Parse(allowManageClosedWorkLogsRows.FirstOrDefault()?.Value);

            if (!allowManageClosedWorkLogs)
            {
                throw new DomainException(
                    $"Month {businessDate.Month:00}/{businessDate.Year} is already closed; work logs in that month cannot be created or edited.");
            }
        }

        workLog.Id = Guid.NewGuid();
        workLog.EmployeeId = employee.Id;
        workLog.Origin = WorkLogOrigin.Manual;
        workLog.CalculateDuration();
        var now = DateTimeOffset.UtcNow;
        workLog.CreatedAtUtc = now;
        workLog.UpdatedAtUtc = now;

        await _employeeWorkLogRepository.AddAsync(workLog, cancellationToken);

        return workLog;
    }
}

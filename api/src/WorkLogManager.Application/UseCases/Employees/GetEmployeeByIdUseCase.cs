using WorkLogManager.Application.Common;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Results;
using WorkLogManager.Application.UseCases.SystemSettings;

namespace WorkLogManager.Application.UseCases.Employees;

public class GetEmployeeByIdUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;
    private readonly GetSystemSettingsUseCase _getSystemSettingsUseCase;

    public GetEmployeeByIdUseCase(
        IEmployeeRepository employeeRepository,
        IEmployeeWorkLogRepository employeeWorkLogRepository,
        GetSystemSettingsUseCase getSystemSettingsUseCase)
    {
        _employeeRepository = employeeRepository;
        _employeeWorkLogRepository = employeeWorkLogRepository;
        _getSystemSettingsUseCase = getSystemSettingsUseCase;
    }

    public async Task<EmployeeDetailResult> ExecuteAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' was not found.");

        var workLogs = await _employeeWorkLogRepository.ListByEmployeeIdAsync(employeeId, cancellationToken);
        var orderedWorkLogs = workLogs.OrderByDescending(w => w.StartDate).ToList();

        var systemSettings = await _getSystemSettingsUseCase.ExecuteAsync(cancellationToken);
        var effectiveDailyWorkHours = employee.DailyWorkHours ?? systemSettings.DefaultDailyWorkHours;

        return new EmployeeDetailResult(employee, orderedWorkLogs, effectiveDailyWorkHours);
    }
}

using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.EmployeeWorkLogs;

public class CreateEmployeeWorkLogUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;
    private readonly IValidator<EmployeeWorkLog> _validator;

    public CreateEmployeeWorkLogUseCase(
        IEmployeeRepository employeeRepository,
        IEmployeeWorkLogRepository employeeWorkLogRepository,
        IValidator<EmployeeWorkLog> validator)
    {
        _employeeRepository = employeeRepository;
        _employeeWorkLogRepository = employeeWorkLogRepository;
        _validator = validator;
    }

    public async Task<EmployeeWorkLog> ExecuteAsync(Guid employeeId, EmployeeWorkLog workLog, CancellationToken cancellationToken = default)
    {
        workLog.EmployeeId = employeeId;
        await _validator.ValidateAndThrowAsync(workLog, cancellationToken);

        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' was not found.");

        workLog.Id = Guid.NewGuid();
        workLog.EmployeeId = employee.Id;
        workLog.CalculateDuration();
        var now = DateTimeOffset.UtcNow;
        workLog.CreatedAtUtc = now;
        workLog.UpdatedAtUtc = now;

        await _employeeWorkLogRepository.AddAsync(workLog, cancellationToken);

        return workLog;
    }
}

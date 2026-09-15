using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Results;

/// <summary>
/// Aggregate result returned by <c>GetEmployeeByIdUseCase</c>, combining the employee,
/// their work logs, and the effective daily work hours (employee-specific or system default).
/// Not a persisted entity - exists purely so the Api has a single object to map to a response DTO.
/// </summary>
public class EmployeeDetailResult
{
    public Employee Employee { get; }
    public IReadOnlyList<EmployeeWorkLog> WorkLogs { get; }
    public decimal EffectiveDailyWorkHours { get; }

    public EmployeeDetailResult(Employee employee, IReadOnlyList<EmployeeWorkLog> workLogs, decimal effectiveDailyWorkHours)
    {
        Employee = employee;
        WorkLogs = workLogs;
        EffectiveDailyWorkHours = effectiveDailyWorkHours;
    }
}

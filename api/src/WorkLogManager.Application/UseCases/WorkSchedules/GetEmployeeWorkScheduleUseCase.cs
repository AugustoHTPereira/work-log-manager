using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.WorkSchedules;

public class GetEmployeeWorkScheduleUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IWorkSchedulePeriodRepository _workSchedulePeriodRepository;

    public GetEmployeeWorkScheduleUseCase(
        IEmployeeRepository employeeRepository,
        IWorkSchedulePeriodRepository workSchedulePeriodRepository)
    {
        _employeeRepository = employeeRepository;
        _workSchedulePeriodRepository = workSchedulePeriodRepository;
    }

    public async Task<IReadOnlyList<WorkSchedulePeriod>> ExecuteAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' was not found.");

        return await _workSchedulePeriodRepository.ListAsync(employee.Id, cancellationToken);
    }
}

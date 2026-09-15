using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.EmployeeWorkLogs;

public class ListEmployeeWorkLogsUseCase
{
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;

    public ListEmployeeWorkLogsUseCase(IEmployeeWorkLogRepository employeeWorkLogRepository)
    {
        _employeeWorkLogRepository = employeeWorkLogRepository;
    }

    public async Task<IReadOnlyList<EmployeeWorkLog>> ExecuteAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var workLogs = await _employeeWorkLogRepository.ListByEmployeeIdAsync(employeeId, cancellationToken);

        return workLogs.OrderByDescending(w => w.StartDate).ToList();
    }
}

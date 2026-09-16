using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.EmployeeWorkLogs;

public class ListEmployeeWorkLogsUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;

    public ListEmployeeWorkLogsUseCase(
        IEmployeeRepository employeeRepository,
        IEmployeeWorkLogRepository employeeWorkLogRepository)
    {
        _employeeRepository = employeeRepository;
        _employeeWorkLogRepository = employeeWorkLogRepository;
    }

    public async Task<IReadOnlyList<EmployeeWorkLog>> ExecuteAsync(
        Guid employeeId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        WorkLogType? type = null,
        WorkLogOrigin? origin = null,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            throw new NotFoundException($"Employee '{employeeId}' was not found.");
        }

        return await _employeeWorkLogRepository.ListByEmployeeIdAsync(
            employeeId,
            startDate,
            endDate,
            type,
            origin,
            cancellationToken);
    }
}

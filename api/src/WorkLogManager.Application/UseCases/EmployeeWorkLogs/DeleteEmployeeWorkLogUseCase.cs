using WorkLogManager.Application.Common;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.EmployeeWorkLogs;

public class DeleteEmployeeWorkLogUseCase
{
    private readonly IEmployeeWorkLogRepository _employeeWorkLogRepository;

    public DeleteEmployeeWorkLogUseCase(IEmployeeWorkLogRepository employeeWorkLogRepository)
    {
        _employeeWorkLogRepository = employeeWorkLogRepository;
    }

    public async Task ExecuteAsync(Guid employeeId, Guid workLogId, CancellationToken cancellationToken = default)
    {
        var workLog = await _employeeWorkLogRepository.GetByIdAsync(workLogId, cancellationToken);
        if (workLog is null || workLog.EmployeeId != employeeId)
        {
            throw new NotFoundException($"Work log '{workLogId}' was not found for employee '{employeeId}'.");
        }

        await _employeeWorkLogRepository.DeleteAsync(workLog, cancellationToken);
    }
}

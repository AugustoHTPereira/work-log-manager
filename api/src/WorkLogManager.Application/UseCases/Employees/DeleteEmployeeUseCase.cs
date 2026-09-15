using WorkLogManager.Application.Common;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.Employees;

public class DeleteEmployeeUseCase
{
    private readonly IEmployeeRepository _employeeRepository;

    public DeleteEmployeeUseCase(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task ExecuteAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' was not found.");

        await _employeeRepository.DeleteAsync(employee, cancellationToken);
    }
}

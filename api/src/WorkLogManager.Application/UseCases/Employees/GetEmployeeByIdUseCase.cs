using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.Employees;

public class GetEmployeeByIdUseCase
{
    private readonly IEmployeeRepository _employeeRepository;

    public GetEmployeeByIdUseCase(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<Employee> ExecuteAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        return await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' was not found.");
    }
}

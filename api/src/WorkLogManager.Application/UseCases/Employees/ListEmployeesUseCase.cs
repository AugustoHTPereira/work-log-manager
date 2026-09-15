using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.Employees;

public class ListEmployeesUseCase
{
    private readonly IEmployeeRepository _employeeRepository;

    public ListEmployeesUseCase(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public Task<IReadOnlyList<Employee>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _employeeRepository.ListAllAsync(cancellationToken);
    }
}

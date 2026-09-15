using FluentValidation;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.Employees;

public class CreateEmployeeUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IValidator<Employee> _validator;

    public CreateEmployeeUseCase(IEmployeeRepository employeeRepository, IValidator<Employee> validator)
    {
        _employeeRepository = employeeRepository;
        _validator = validator;
    }

    public async Task<Employee> ExecuteAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(employee, cancellationToken);

        employee.Id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        employee.CreatedAtUtc = now;
        employee.UpdatedAtUtc = now;

        await _employeeRepository.AddAsync(employee, cancellationToken);

        return employee;
    }
}

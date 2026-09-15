using FluentValidation;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.UseCases.Employees;

public class UpdateEmployeeUseCase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IValidator<Employee> _validator;

    public UpdateEmployeeUseCase(IEmployeeRepository employeeRepository, IValidator<Employee> validator)
    {
        _employeeRepository = employeeRepository;
        _validator = validator;
    }

    /// <summary>
    /// Applies the field values carried by <paramref name="employee"/> (typically mapped
    /// straight from the Api request) onto the persisted employee identified by
    /// <paramref name="employeeId"/>.
    /// </summary>
    public async Task<Employee> ExecuteAsync(Guid employeeId, Employee employee, CancellationToken cancellationToken = default)
    {
        employee.Id = employeeId;
        await _validator.ValidateAndThrowAsync(employee, cancellationToken);

        var existingEmployee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee '{employeeId}' was not found.");

        existingEmployee.Name = employee.Name;
        existingEmployee.Role = employee.Role;
        existingEmployee.HireDate = employee.HireDate;
        existingEmployee.DailyWorkHours = employee.DailyWorkHours;
        existingEmployee.Touch();

        await _employeeRepository.UpdateAsync(existingEmployee, cancellationToken);

        return existingEmployee;
    }
}

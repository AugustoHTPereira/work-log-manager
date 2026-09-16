using FluentValidation;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Validators;

/// <summary>
/// Validates the "user input" invariants of an <see cref="Employee"/> (name/role required).
/// Shared by both <c>CreateEmployeeUseCase</c> and <c>UpdateEmployeeUseCase</c>, since both
/// receive a fully-populated <see cref="Employee"/> and the input rules are identical for
/// create and update.
/// </summary>
public class EmployeeValidator : AbstractValidator<Employee>
{
    public EmployeeValidator()
    {
        RuleFor(e => e.Name)
            .NotEmpty()
            .WithMessage("Employee name is required.");

        RuleFor(e => e.Role)
            .NotEmpty()
            .WithMessage("Employee role is required.");
    }
}

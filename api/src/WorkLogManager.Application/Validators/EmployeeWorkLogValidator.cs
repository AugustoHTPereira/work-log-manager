using FluentValidation;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Validators;

/// <summary>
/// Validates the "user input" invariants of an <see cref="EmployeeWorkLog"/> (end date must
/// not be earlier than the start date). Shared by both
/// <c>CreateEmployeeWorkLogUseCase</c> and <c>UpdateEmployeeWorkLogUseCase</c>.
/// </summary>
public class EmployeeWorkLogValidator : AbstractValidator<EmployeeWorkLog>
{
    public EmployeeWorkLogValidator()
    {
        RuleFor(w => w.EndDate)
            .GreaterThanOrEqualTo(w => w.StartDate)
            .WithMessage("Work log end date must not be earlier than the start date.");

        RuleFor(w => w.Note)
            .MaximumLength(255)
            .WithMessage("Work log note must not exceed 255 characters.")
            .When(w => !string.IsNullOrEmpty(w.Note));
    }
}

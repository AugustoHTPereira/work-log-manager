using FluentValidation;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Validators;

/// <summary>
/// Validates the "user input" invariants of a single <see cref="WorkSchedulePeriod"/> (valid
/// day of week, end time after start time). Shared by <c>UpdateGeneralWorkScheduleUseCase</c>
/// and <c>UpdateEmployeeWorkScheduleUseCase</c>, since both receive a fully-populated
/// <see cref="WorkSchedulePeriod"/> per item and the input rules are identical for the general
/// and employee-specific configurations.
/// </summary>
public class WorkSchedulePeriodValidator : AbstractValidator<WorkSchedulePeriod>
{
    public WorkSchedulePeriodValidator()
    {
        RuleFor(p => p.DayOfWeek)
            .IsInEnum()
            .WithMessage("Day of week must be a valid value.");

        RuleFor(p => p.EndTime)
            .GreaterThan(p => p.StartTime)
            .WithMessage("End time must be greater than start time.");
    }
}

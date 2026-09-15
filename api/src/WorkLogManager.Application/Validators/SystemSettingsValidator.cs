using FluentValidation;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Validators;

/// <summary>
/// Validates the "user input" invariants of <see cref="SystemSettings"/> (default daily
/// work hours greater than zero). Used by <c>UpdateSystemSettingsUseCase</c>.
/// </summary>
public class SystemSettingsValidator : AbstractValidator<SystemSettings>
{
    public SystemSettingsValidator()
    {
        RuleFor(s => s.DefaultDailyWorkHours)
            .GreaterThan(0)
            .WithMessage("Default daily work hours must be greater than zero.");
    }
}

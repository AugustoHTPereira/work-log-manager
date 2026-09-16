using FluentValidation;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Validators;

/// <summary>
/// Validates the "user input" invariants of a single <see cref="SystemParameter"/> row before it
/// is persisted by <c>UpdateSystemParameterUseCase</c> (scalar parameters) or
/// <c>AddSystemParameterValueUseCase</c> (one row of an array parameter).
/// <see cref="SystemParameter.Value"/> is an opaque string, but its shape must still respect the
/// parameter's logical type (see <see cref="Services.SystemParameterCatalog"/>).
/// </summary>
public class SystemParameterValidator : AbstractValidator<SystemParameter>
{
    private static readonly HashSet<WorkLogType> AllowedAutoWorkLogTypes =
        new() { WorkLogType.RegularAttendance, WorkLogType.Break };

    public SystemParameterValidator()
    {
        RuleFor(p => p.Value)
            .NotEmpty()
            .WithMessage("Value is required.");

        RuleFor(p => p.Value)
            .Must(value => bool.TryParse(value, out _))
            .WithMessage("Value must be 'true' or 'false'.")
            .When(p => p.Param == SystemParameterName.AllowManageClosedWorkLogs && !string.IsNullOrEmpty(p.Value));

        RuleFor(p => p.Value)
            .Must(BeAllowedAutoWorkLogType)
            .WithMessage("Value must be one of 'RegularAttendance' or 'Break'.")
            .When(p => p.Param == SystemParameterName.AutoWorkLogTypes && !string.IsNullOrEmpty(p.Value));
    }

    private static bool BeAllowedAutoWorkLogType(string value)
    {
        return Enum.TryParse<WorkLogType>(value, out var type) && AllowedAutoWorkLogTypes.Contains(type);
    }
}

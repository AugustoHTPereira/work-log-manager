using FluentValidation;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Validators;

/// <summary>
/// Validates the "user input" invariants of a <see cref="MonthClosing"/> (month/year range,
/// and that only a fully completed past month can be closed). Used by
/// <c>CloseMonthUseCase</c>.
/// </summary>
public class MonthClosingValidator : AbstractValidator<MonthClosing>
{
    public MonthClosingValidator()
    {
        RuleFor(m => m.Month)
            .InclusiveBetween(1, 12);

        RuleFor(m => m.Year)
            .GreaterThan(0);

        RuleFor(m => m)
            .Must(BeStrictlyBeforeCurrentMonth)
            .WithMessage("Only fully completed past months can be closed.");
    }

    private static bool BeStrictlyBeforeCurrentMonth(MonthClosing monthClosing)
    {
        var now = DateTimeOffset.UtcNow;
        var currentMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        if (monthClosing.Month is < 1 or > 12 || monthClosing.Year is < 1 or > 9999)
        {
            // Out-of-range month/year is already reported by the dedicated rules above;
            // avoid constructing an invalid DateTimeOffset here.
            return false;
        }

        var closingMonthStart = new DateTimeOffset(monthClosing.Year, monthClosing.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return closingMonthStart < currentMonthStart;
    }
}

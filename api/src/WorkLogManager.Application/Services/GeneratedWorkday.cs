namespace WorkLogManager.Application.Services;

/// <summary>
/// The result of <see cref="WorkLogGenerationService.GenerateWorkday"/>: the attendance
/// period(s) and, optionally, the break period for a single generated workday.
/// </summary>
public record GeneratedWorkday
{
    public IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> RegularAttendancePeriods { get; init; } = [];
    public (DateTimeOffset Start, DateTimeOffset End)? BreakPeriod { get; init; }

    public static GeneratedWorkday WithoutBreak(DateTimeOffset start, DateTimeOffset end)
    {
        return new GeneratedWorkday
        {
            RegularAttendancePeriods = [(start, end)],
            BreakPeriod = null,
        };
    }

    public static GeneratedWorkday WithBreak(
        DateTimeOffset morningStart,
        DateTimeOffset morningEnd,
        DateTimeOffset breakStart,
        DateTimeOffset breakEnd,
        DateTimeOffset afternoonStart,
        DateTimeOffset afternoonEnd)
    {
        return new GeneratedWorkday
        {
            RegularAttendancePeriods = [(morningStart, morningEnd), (afternoonStart, afternoonEnd)],
            BreakPeriod = (breakStart, breakEnd),
        };
    }
}

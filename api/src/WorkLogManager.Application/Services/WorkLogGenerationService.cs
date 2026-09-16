using WorkLogManager.Application.Interfaces;

namespace WorkLogManager.Application.Services;

/// <summary>
/// Pure domain service (except for the injected <see cref="IRandomProvider"/>) that
/// calculates the attendance/break periods of a single generated workday, given a date and
/// the employee's effective daily work hours.
/// </summary>
/// <remarks>
/// Base workday start is <c>07:00</c>, base break window is <c>11:00</c>-<c>12:00</c>, both in
/// <see cref="BusinessTimeZone"/> (business local time), not UTC.
/// Both are varied by a pseudo-random offset of up to <see cref="MaxVarianceMinutes"/> minutes,
/// never the base time itself. See the "Regra de geração dos períodos" section of the
/// approved plan for the full mathematical proof behind the <c>endOffsetMinutes</c> formula
/// below: it guarantees the total worked duration (morning + afternoon) is never below
/// <c>effectiveDailyWorkHours</c>, even in the presence of break variance.
/// </remarks>
public class WorkLogGenerationService
{
    private const int MaxVarianceMinutes = 4;
    private static readonly TimeOnly WorkdayBaseStart = new(7, 0);
    private static readonly TimeOnly BreakStart = new(11, 0);
    private static readonly TimeOnly BreakEnd = new(12, 0);

    private readonly IRandomProvider _randomProvider;

    public WorkLogGenerationService(IRandomProvider randomProvider)
    {
        _randomProvider = randomProvider;
    }

    public GeneratedWorkday GenerateWorkday(DateOnly date, decimal effectiveDailyWorkHours)
    {
        var baseStart = BusinessTimeZone.ToInstant(date, WorkdayBaseStart);
        var breakStart = BusinessTimeZone.ToInstant(date, BreakStart);
        var breakEnd = BusinessTimeZone.ToInstant(date, BreakEnd);

        var startOffsetMinutes = _randomProvider.NextInt(-MaxVarianceMinutes, MaxVarianceMinutes);

        if (effectiveDailyWorkHours <= 4m)
        {
            var shortWorkdayEndOffsetMinutes = _randomProvider.NextInt(startOffsetMinutes, MaxVarianceMinutes);
            var singlePeriodEnd = baseStart.AddHours((double)effectiveDailyWorkHours).AddMinutes(shortWorkdayEndOffsetMinutes);
            return GeneratedWorkday.WithoutBreak(baseStart.AddMinutes(startOffsetMinutes), singlePeriodEnd);
        }

        var breakStartOffsetMinutes = _randomProvider.NextInt(-MaxVarianceMinutes, MaxVarianceMinutes);
        var breakEndOffsetMinutes = _randomProvider.NextInt(breakStartOffsetMinutes, MaxVarianceMinutes);

        // Floor of the end-of-workday offset: compensates the break's variance to guarantee
        // morning + afternoon never fall below the effective daily work hours.
        var requiredEndOffsetMinutes = startOffsetMinutes - breakStartOffsetMinutes + breakEndOffsetMinutes;
        var endOffsetUpperBound = Math.Max(requiredEndOffsetMinutes, MaxVarianceMinutes);
        var endOffsetMinutes = _randomProvider.NextInt(requiredEndOffsetMinutes, endOffsetUpperBound);

        var morningEnd = breakStart.AddMinutes(breakStartOffsetMinutes);
        var breakEndAdjusted = breakEnd.AddMinutes(breakEndOffsetMinutes);
        var afternoonEnd = breakEnd.AddHours((double)(effectiveDailyWorkHours - 4m)).AddMinutes(endOffsetMinutes);

        return GeneratedWorkday.WithBreak(
            morningStart: baseStart.AddMinutes(startOffsetMinutes),
            morningEnd: morningEnd,
            breakStart: morningEnd,
            breakEnd: breakEndAdjusted,
            afternoonStart: breakEndAdjusted,
            afternoonEnd: afternoonEnd);
    }
}

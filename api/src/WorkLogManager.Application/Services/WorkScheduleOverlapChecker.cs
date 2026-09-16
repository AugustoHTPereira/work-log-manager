using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Services;

/// <summary>
/// Stateless domain service that checks a full list of periods being persisted for a single
/// owner (general configuration or a specific employee) for overlapping periods within the
/// same <see cref="WorkSchedulePeriod.DayOfWeek"/>. Two periods overlap when
/// <c>a.StartTime &lt; b.EndTime &amp;&amp; b.StartTime &lt; a.EndTime</c>.
/// </summary>
public static class WorkScheduleOverlapChecker
{
    public static void EnsureNoOverlap(IReadOnlyList<WorkSchedulePeriod> periods)
    {
        var periodsByDay = periods.GroupBy(p => p.DayOfWeek);

        foreach (var dayPeriods in periodsByDay)
        {
            var orderedPeriods = dayPeriods.ToList();

            for (var i = 0; i < orderedPeriods.Count; i++)
            {
                for (var j = i + 1; j < orderedPeriods.Count; j++)
                {
                    var a = orderedPeriods[i];
                    var b = orderedPeriods[j];

                    if (a.StartTime < b.EndTime && b.StartTime < a.EndTime)
                    {
                        throw new DomainException(
                            $"Work schedule periods overlap on {a.DayOfWeek}: {a.StartTime}-{a.EndTime} and {b.StartTime}-{b.EndTime}.");
                    }
                }
            }
        }
    }
}

using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Services;

/// <summary>
/// Pure domain service that resolves the effective work schedule of a single day, applying
/// the "employee overrides general by whole day" rule: if the employee has any period
/// registered for that day of week, the general configuration for that day is entirely
/// ignored; otherwise the general configuration for that day applies; if neither has a
/// period, the day is treated as a day off.
/// </summary>
public static class WorkScheduleResolver
{
    public static IReadOnlyList<WorkSchedulePeriod> GetEffectivePeriods(
        DayOfWeek dayOfWeek,
        IReadOnlyList<WorkSchedulePeriod> generalPeriods,
        IReadOnlyList<WorkSchedulePeriod> employeePeriods)
    {
        var employeeDayPeriods = employeePeriods.Where(p => p.DayOfWeek == dayOfWeek).ToList();
        return employeeDayPeriods.Count > 0
            ? employeeDayPeriods
            : generalPeriods.Where(p => p.DayOfWeek == dayOfWeek).ToList();
    }

    public static decimal GetEffectiveDailyWorkHours(
        DayOfWeek dayOfWeek,
        IReadOnlyList<WorkSchedulePeriod> generalPeriods,
        IReadOnlyList<WorkSchedulePeriod> employeePeriods)
    {
        var periods = GetEffectivePeriods(dayOfWeek, generalPeriods, employeePeriods);
        return periods.Sum(p => (decimal)(p.EndTime - p.StartTime).TotalHours);
    }
}

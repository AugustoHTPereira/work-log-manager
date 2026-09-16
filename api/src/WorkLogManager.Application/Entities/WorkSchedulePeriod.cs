namespace WorkLogManager.Application.Entities;

/// <summary>
/// A single work schedule period (a day of week + start/end time), either belonging to the
/// general (company-wide) configuration when <see cref="EmployeeId"/> is <c>null</c>, or to a
/// specific employee's configuration otherwise. A given owner (general or a specific employee)
/// can have multiple periods for the same <see cref="DayOfWeek"/> (e.g. morning and afternoon).
/// </summary>
/// <remarks>
/// Post-review architectural note: see <see cref="Employee"/> for the general rationale
/// (validation moved to FluentValidation validators at the use-case boundary; plain property
/// bag with <c>internal</c> setters).
/// </remarks>
public class WorkSchedulePeriod
{
    public Guid Id { get; internal set; }

    /// <summary>
    /// <c>null</c> represents the general (company-wide) configuration; a value represents a
    /// period specific to that employee.
    /// </summary>
    public Guid? EmployeeId { get; internal set; }

    public DayOfWeek DayOfWeek { get; internal set; }
    public TimeOnly StartTime { get; internal set; }
    public TimeOnly EndTime { get; internal set; }
    public DateTimeOffset CreatedAtUtc { get; internal set; }
    public DateTimeOffset UpdatedAtUtc { get; internal set; }

    /// <summary>
    /// Refreshes <see cref="UpdatedAtUtc"/> to the current UTC instant.
    /// </summary>
    public void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

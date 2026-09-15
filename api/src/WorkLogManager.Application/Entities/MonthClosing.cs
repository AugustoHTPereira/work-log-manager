namespace WorkLogManager.Application.Entities;

/// <summary>
/// Represents the "closing" of a calendar month: the event of automatically generating
/// attendance work logs for every employee for that month. Unique per (<see cref="Year"/>,
/// <see cref="Month"/>).
/// </summary>
/// <remarks>
/// Post-review architectural note: see <see cref="Employee"/> for the general rationale
/// (validation moved to FluentValidation validators at the use-case boundary).
/// </remarks>
public class MonthClosing
{
    public Guid Id { get; internal set; }
    public int Month { get; internal set; }
    public int Year { get; internal set; }
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

using WorkLogManager.Application.Common;

namespace WorkLogManager.Application.Entities;

/// <summary>
/// A recorded absence or overtime period for an <see cref="Employee"/>.
/// </summary>
/// <remarks>
/// Post-review architectural note: see <see cref="Employee"/> for the general rationale
/// (validation moved to FluentValidation validators at the use-case boundary). This entity
/// keeps one piece of domain behavior beyond a plain property bag: <see cref="CalculateDuration"/>,
/// which derives <see cref="DurationSeconds"/> from <see cref="StartDate"/>/<see cref="EndDate"/>
/// and still guards against a negative duration. That guard is intentionally kept as
/// defense-in-depth (the same rule is also expressed in
/// <see cref="Validators.EmployeeWorkLogValidator"/>) because <see cref="DurationSeconds"/> is a
/// derived/computed value, not raw user input, so it is a core domain invariant rather than a
/// simple input-shape rule.
/// </remarks>
public class EmployeeWorkLog
{
    public Guid Id { get; internal set; }
    public Guid EmployeeId { get; internal set; }
    public WorkLogType Type { get; internal set; }
    public DateTimeOffset StartDate { get; internal set; }
    public DateTimeOffset EndDate { get; internal set; }
    public long DurationSeconds { get; internal set; }
    public DateTimeOffset CreatedAtUtc { get; internal set; }
    public DateTimeOffset UpdatedAtUtc { get; internal set; }

    /// <summary>
    /// Recalculates <see cref="DurationSeconds"/> from <see cref="StartDate"/>/<see cref="EndDate"/>.
    /// </summary>
    public void CalculateDuration()
    {
        if (EndDate < StartDate)
        {
            throw new DomainException("Work log end date must not be earlier than the start date.");
        }

        DurationSeconds = (long)(EndDate - StartDate).TotalSeconds;
    }

    /// <summary>
    /// Refreshes <see cref="UpdatedAtUtc"/> to the current UTC instant.
    /// </summary>
    public void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

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
    /// Optional link to the <see cref="MonthClosing"/> this work log belongs to. Set either
    /// when the "close month" flow generates a <see cref="WorkLogType.RegularAttendance"/>/
    /// <see cref="WorkLogType.Break"/> record, or when that same flow associates a
    /// pre-existing (e.g. manually created <see cref="WorkLogType.Overtime"/>/
    /// <see cref="WorkLogType.Absence"/>) work log whose <see cref="StartDate"/> falls within
    /// the month being closed. Once set, the work log is considered "frozen": it can no
    /// longer be edited or deleted. <c>null</c> for work logs not yet linked to any closed
    /// month.
    /// </summary>
    public Guid? MonthClosingId { get; internal set; }

    /// <summary>
    /// Optional free-text remark about this work log (e.g. the reason for an absence).
    /// <c>null</c> or empty when not provided.
    /// </summary>
    public string? Note { get; internal set; }

    /// <summary>
    /// Whether this work log was generated automatically by the "close month" flow or
    /// created manually by a user. See <see cref="WorkLogOrigin"/>.
    /// </summary>
    public WorkLogOrigin Origin { get; internal set; }

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

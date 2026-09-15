namespace WorkLogManager.Application.Entities;

/// <summary>
/// System-wide singleton settings. Only one row ever exists, identified by <see cref="SingletonId"/>.
/// </summary>
/// <remarks>
/// Post-review architectural note: see <see cref="Employee"/> for the general rationale
/// (validation moved to FluentValidation validators at the use-case boundary).
/// </remarks>
public class SystemSettings
{
    /// <summary>
    /// Fixed, well-known id for the single row of this table.
    /// </summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; internal set; } = SingletonId;
    public decimal DefaultDailyWorkHours { get; internal set; }
    public DateTimeOffset UpdatedAtUtc { get; internal set; }

    /// <summary>
    /// Refreshes <see cref="UpdatedAtUtc"/> to the current UTC instant.
    /// </summary>
    public void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

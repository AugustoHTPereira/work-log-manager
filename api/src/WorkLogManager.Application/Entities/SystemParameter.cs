namespace WorkLogManager.Application.Entities;

/// <summary>
/// A single row of a system-wide configuration parameter. For scalar parameters (e.g.
/// <see cref="SystemParameterValueType.Bool"/>) at most one row exists per <see cref="Param"/>.
/// For <see cref="SystemParameterValueType.Array"/> parameters, each enabled value is stored
/// as its own row (same <see cref="Param"/>, different <see cref="Value"/>) instead of a single
/// delimited string; the unique index in Infrastructure is therefore on <c>(Param, Value)</c>,
/// not on <see cref="Param"/> alone. The back-end never interprets <see cref="Value"/> as a
/// strong type; it is an opaque string whose shape is only described by <see cref="ValueType"/>
/// (see <see cref="Services.SystemParameterCatalog"/>) for the front-end's benefit.
/// </summary>
/// <remarks>
/// Post-review architectural note: see <see cref="Employee"/> for the general rationale
/// (plain property bag with <c>internal</c> setters, validation moved to FluentValidation
/// validators at the use-case boundary).
/// </remarks>
public class SystemParameter
{
    public Guid Id { get; internal set; }
    public SystemParameterName Param { get; internal set; }
    public string Value { get; internal set; } = string.Empty;
    public SystemParameterValueType ValueType { get; internal set; }
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

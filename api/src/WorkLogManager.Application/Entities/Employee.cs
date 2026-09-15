namespace WorkLogManager.Application.Entities;

/// <summary>
/// An employee that can have work logs (absences/overtime) recorded against them.
/// </summary>
/// <remarks>
/// Post-review architectural note: this entity used to protect its invariants (name/role
/// required, <see cref="DailyWorkHours"/> &gt; 0 when informed) in a validating constructor
/// and <c>Update</c> method, which prevented AutoMapper from mapping Api requests directly
/// onto it. Validation of those "user input" rules now lives in
/// <see cref="Validators.EmployeeValidator"/> (FluentValidation), invoked explicitly by the
/// use-cases before persisting. The entity itself stays intentionally simple (a plain
/// property bag with <c>internal</c> setters, only mutable from within the
/// <c>Application</c> assembly) plus a small <see cref="Touch"/> helper to keep the
/// audit timestamp convention centralized in one place instead of duplicated across
/// use-cases.
/// </remarks>
public class Employee
{
    public Guid Id { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public string Role { get; internal set; } = string.Empty;
    public DateOnly HireDate { get; internal set; }
    public decimal? DailyWorkHours { get; internal set; }
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

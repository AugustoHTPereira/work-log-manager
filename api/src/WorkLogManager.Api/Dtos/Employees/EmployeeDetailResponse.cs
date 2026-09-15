using WorkLogManager.Api.Dtos.EmployeeWorkLogs;

namespace WorkLogManager.Api.Dtos.Employees;

/// <summary>
/// Declared with init-only properties (instead of a positional record) so AutoMapper maps
/// members by name/<c>ForMember</c> instead of trying to match a positional constructor.
/// </summary>
public record EmployeeDetailResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateOnly HireDate { get; init; }
    public decimal? DailyWorkHours { get; init; }
    public decimal EffectiveDailyWorkHours { get; init; }
    public IReadOnlyList<EmployeeWorkLogResponse> WorkLogs { get; init; } = [];
}

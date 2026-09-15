namespace WorkLogManager.Api.Dtos.MonthClosings;

/// <summary>
/// Declared with init-only properties (instead of a positional record) so AutoMapper maps
/// members by name/<c>ForMember</c> instead of trying to match a positional constructor -
/// same convention used by <c>EmployeeDetailResponse</c>, since this combines fields of
/// <c>MonthClosing</c> with the <c>Summaries</c> list from <c>MonthClosingResult</c>.
/// </summary>
public record MonthClosingResponse
{
    public Guid Id { get; init; }
    public int Month { get; init; }
    public int Year { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public IReadOnlyList<EmployeeWorkLogGenerationSummaryResponse> Summaries { get; init; } = [];
}

namespace WorkLogManager.Api.Dtos.MonthClosings;

/// <summary>
/// <see cref="GeneratedCount"/>/<see cref="SkippedCount"/> count business days, not raw
/// <c>EmployeeWorkLog</c> rows - same semantics as
/// <see cref="Application.Results.EmployeeWorkLogGenerationSummary"/>.
/// </summary>
public record EmployeeWorkLogGenerationSummaryResponse(Guid EmployeeId, string EmployeeName, int GeneratedCount, int SkippedCount);

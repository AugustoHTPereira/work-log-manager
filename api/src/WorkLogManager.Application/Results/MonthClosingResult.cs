using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Results;

/// <summary>
/// Aggregate result returned by <c>CloseMonthUseCase</c>, combining the created
/// <see cref="MonthClosing"/> with a per-employee generation summary. Not a persisted
/// entity - exists purely so the Api has a single object to map to a response DTO.
/// </summary>
public class MonthClosingResult
{
    public MonthClosing MonthClosing { get; }
    public IReadOnlyList<EmployeeWorkLogGenerationSummary> Summaries { get; }

    public MonthClosingResult(MonthClosing monthClosing, IReadOnlyList<EmployeeWorkLogGenerationSummary> summaries)
    {
        MonthClosing = monthClosing;
        Summaries = summaries;
    }
}

/// <summary>
/// Per-employee summary of a month closing. <see cref="GeneratedCount"/>/<see cref="SkippedCount"/>
/// count business days processed (generated vs. skipped), not the raw number of
/// <see cref="EmployeeWorkLog"/> rows inserted - a single generated day may insert 1 row
/// (daily work hours &lt;= 4h, no break) or 3 rows (&gt; 4h: morning + break + afternoon), which
/// would be a confusing detail to surface in a business-facing summary.
/// </summary>
public class EmployeeWorkLogGenerationSummary
{
    public Guid EmployeeId { get; }
    public string EmployeeName { get; }
    public int GeneratedCount { get; }
    public int SkippedCount { get; }

    public EmployeeWorkLogGenerationSummary(Guid employeeId, string employeeName, int generatedCount, int skippedCount)
    {
        EmployeeId = employeeId;
        EmployeeName = employeeName;
        GeneratedCount = generatedCount;
        SkippedCount = skippedCount;
    }
}

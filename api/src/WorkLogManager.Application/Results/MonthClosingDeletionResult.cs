namespace WorkLogManager.Application.Results;

/// <summary>
/// Result of <c>DeleteMonthClosingUseCase</c>, summarizing what happened to the
/// <see cref="Entities.EmployeeWorkLog"/>s that were linked to the deleted
/// <see cref="Entities.MonthClosing"/> - exists purely so the Api has a single object to map to
/// a response DTO.
/// </summary>
public class MonthClosingDeletionResult
{
    public Guid MonthClosingId { get; }
    public int Month { get; }
    public int Year { get; }
    public int DeletedAutomaticWorkLogsCount { get; }
    public int UnlinkedManualWorkLogsCount { get; }

    public MonthClosingDeletionResult(
        Guid monthClosingId,
        int month,
        int year,
        int deletedAutomaticWorkLogsCount,
        int unlinkedManualWorkLogsCount)
    {
        MonthClosingId = monthClosingId;
        Month = month;
        Year = year;
        DeletedAutomaticWorkLogsCount = deletedAutomaticWorkLogsCount;
        UnlinkedManualWorkLogsCount = unlinkedManualWorkLogsCount;
    }
}

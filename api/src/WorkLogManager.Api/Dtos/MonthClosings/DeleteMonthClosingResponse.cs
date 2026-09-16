namespace WorkLogManager.Api.Dtos.MonthClosings;

public record DeleteMonthClosingResponse
{
    public Guid MonthClosingId { get; init; }
    public int Month { get; init; }
    public int Year { get; init; }
    public int DeletedAutomaticWorkLogsCount { get; init; }
    public int UnlinkedManualWorkLogsCount { get; init; }
}

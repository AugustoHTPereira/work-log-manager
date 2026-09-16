namespace WorkLogManager.Api.Dtos.WorkSchedules;

public record UpdateWorkScheduleRequest(IReadOnlyList<WorkSchedulePeriodInputDto> Periods);

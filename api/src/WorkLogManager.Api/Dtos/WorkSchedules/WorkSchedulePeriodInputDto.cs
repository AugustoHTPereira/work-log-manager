namespace WorkLogManager.Api.Dtos.WorkSchedules;

public record WorkSchedulePeriodInputDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

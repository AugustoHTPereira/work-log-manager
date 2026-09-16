namespace WorkLogManager.Api.Dtos.WorkSchedules;

public record WorkSchedulePeriodResponse(Guid Id, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

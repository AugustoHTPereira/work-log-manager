using WorkLogManager.Application.Entities;

namespace WorkLogManager.Api.Dtos.EmployeeWorkLogs;

public record CreateEmployeeWorkLogRequest(WorkLogType Type, DateTimeOffset StartDate, DateTimeOffset EndDate);

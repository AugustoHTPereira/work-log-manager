using WorkLogManager.Application.Entities;

namespace WorkLogManager.Api.Dtos.EmployeeWorkLogs;

public record UpdateEmployeeWorkLogRequest(WorkLogType Type, DateTimeOffset StartDate, DateTimeOffset EndDate, string? Note);

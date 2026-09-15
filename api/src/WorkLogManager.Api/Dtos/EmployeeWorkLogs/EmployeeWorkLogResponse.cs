using WorkLogManager.Application.Entities;

namespace WorkLogManager.Api.Dtos.EmployeeWorkLogs;

public record EmployeeWorkLogResponse(
    Guid Id,
    Guid EmployeeId,
    WorkLogType Type,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    long DurationSeconds);

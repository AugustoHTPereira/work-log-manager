using WorkLogManager.Application.Entities;

namespace WorkLogManager.Api.Dtos.SystemParameters;

public record SystemParameterResponse(
    Guid Id,
    SystemParameterName Param,
    string Value,
    SystemParameterValueType ValueType,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

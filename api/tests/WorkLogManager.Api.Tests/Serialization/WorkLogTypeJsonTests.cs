using System.Text.Json;
using System.Text.Json.Serialization;
using WorkLogManager.Api.Dtos.EmployeeWorkLogs;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Api.Tests.Serialization;

/// <summary>
/// Verifies that the JSON serializer options mirroring the API's
/// <c>ConfigureHttpJsonOptions</c> registration correctly read and write
/// <see cref="WorkLogType"/> as a string instead of the default numeric value.
/// </summary>
public class WorkLogTypeJsonTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        // Mirrors the defaults of ASP.NET Core's Microsoft.AspNetCore.Http.Json.JsonOptions
        // (camelCase property names, case-insensitive matching), which is the options
        // instance mutated by ConfigureHttpJsonOptions in Program.cs.
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Fact]
    public void Deserialize_CreateEmployeeWorkLogRequest_WithStringType_ParsesEnum()
    {
        const string json = """
            {"type":"Absence","startDate":"2026-01-01T08:00:00+00:00","endDate":"2026-01-01T09:00:00+00:00"}
            """;

        var result = JsonSerializer.Deserialize<CreateEmployeeWorkLogRequest>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(WorkLogType.Absence, result.Type);
    }

    [Fact]
    public void Deserialize_CreateEmployeeWorkLogRequest_WithLowerCaseType_IsCaseInsensitive()
    {
        const string json = """
            {"type":"absence","startDate":"2026-01-01T08:00:00+00:00","endDate":"2026-01-01T09:00:00+00:00"}
            """;

        var result = JsonSerializer.Deserialize<CreateEmployeeWorkLogRequest>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(WorkLogType.Absence, result.Type);
    }

    [Fact]
    public void Deserialize_UpdateEmployeeWorkLogRequest_WithStringType_ParsesEnum()
    {
        const string json = """
            {"type":"Overtime","startDate":"2026-01-01T08:00:00+00:00","endDate":"2026-01-01T09:00:00+00:00"}
            """;

        var result = JsonSerializer.Deserialize<UpdateEmployeeWorkLogRequest>(json, Options);

        Assert.NotNull(result);
        Assert.Equal(WorkLogType.Overtime, result.Type);
    }

    [Fact]
    public void Deserialize_CreateEmployeeWorkLogRequest_WithInvalidType_ThrowsJsonException()
    {
        const string json = """
            {"type":"Invalid","startDate":"2026-01-01T08:00:00+00:00","endDate":"2026-01-01T09:00:00+00:00"}
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateEmployeeWorkLogRequest>(json, Options));
    }

    [Fact]
    public void Serialize_EmployeeWorkLogResponse_WritesTypeAsString()
    {
        var response = new EmployeeWorkLogResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkLogType.Absence,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            3600);

        var json = JsonSerializer.Serialize(response, Options);

        Assert.Contains("\"type\":\"Absence\"", json);
    }
}

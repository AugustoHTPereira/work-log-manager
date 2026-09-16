namespace WorkLogManager.Application.Services;

/// <summary>
/// Resolves the raw <see cref="Entities.SystemParameter.Value"/> of
/// <see cref="Entities.SystemParameterName.AllowManageClosedWorkLogs"/> into a bool. Centralizes
/// the fallback (no configuration, empty, or unparseable value -> <c>false</c>) in a single,
/// testable place shared by <see cref="UseCases.EmployeeWorkLogs.CreateEmployeeWorkLogUseCase"/>,
/// <see cref="UseCases.EmployeeWorkLogs.UpdateEmployeeWorkLogUseCase"/> and
/// <see cref="UseCases.EmployeeWorkLogs.DeleteEmployeeWorkLogUseCase"/>.
/// </summary>
public static class AllowManageClosedWorkLogsParser
{
    public static bool Parse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        return bool.TryParse(rawValue, out var value) && value;
    }
}

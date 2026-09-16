namespace WorkLogManager.Application.Entities;

/// <summary>
/// The set of known system-wide configuration parameters. Closed on purpose: new
/// parameters are added here explicitly (and to <see cref="Services.SystemParameterCatalog"/>)
/// as new tasks require them.
/// </summary>
public enum SystemParameterName
{
    AutoWorkLogTypes,
    AllowManageClosedWorkLogs
}

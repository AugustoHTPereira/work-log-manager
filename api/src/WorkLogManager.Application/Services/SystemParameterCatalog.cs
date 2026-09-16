using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Services;

/// <summary>
/// Closed, static mapping of every known <see cref="SystemParameterName"/> to its logical
/// <see cref="SystemParameterValueType"/>. Pure domain service, no state - the server-side
/// source of truth for a parameter's shape, so <see cref="SystemParameter.ValueType"/> never
/// has to be (and never is) supplied by the client.
/// </summary>
public static class SystemParameterCatalog
{
    private static readonly IReadOnlyDictionary<SystemParameterName, SystemParameterValueType> ValueTypesByParam =
        new Dictionary<SystemParameterName, SystemParameterValueType>
        {
            [SystemParameterName.AutoWorkLogTypes] = SystemParameterValueType.Array,
            [SystemParameterName.AllowManageClosedWorkLogs] = SystemParameterValueType.Bool,
        };

    public static SystemParameterValueType GetValueType(SystemParameterName param) => ValueTypesByParam[param];
}

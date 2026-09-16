using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Services;

/// <summary>
/// Resolves the <see cref="SystemParameterName.AutoWorkLogTypes"/> rows (one row per enabled
/// <see cref="WorkLogType"/>) into the set of <see cref="WorkLogType"/> that
/// <see cref="UseCases.MonthClosings.CloseMonthUseCase"/> should persist when closing a month.
/// Centralizes the fallback (no configuration -> only <see cref="WorkLogType.RegularAttendance"/>)
/// in a single, testable place.
/// </summary>
public static class AutoWorkLogTypesParser
{
    public static IReadOnlySet<WorkLogType> Parse(IReadOnlyCollection<SystemParameter> rows)
    {
        if (rows.Count == 0)
        {
            return new HashSet<WorkLogType> { WorkLogType.RegularAttendance };
        }

        return rows
            .Select(row => Enum.Parse<WorkLogType>(row.Value))
            .ToHashSet();
    }
}

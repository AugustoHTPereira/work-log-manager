using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Services;

/// <summary>
/// Maps each <see cref="WorkLogType"/> to the short abbreviation used in the month closing
/// PDF report (see <c>GenerateMonthClosingReportUseCase</c> and
/// <c>MonthClosingReportRenderer</c>). Abbreviations were defined by the product owner
/// (<c>RegularAttendance</c>/<c>Overtime</c>) or chosen for clarity in a Brazilian HR report
/// context (<c>Absence</c>/<c>Break</c>) - see the approved development plan for this task.
/// </summary>
public static class WorkLogTypeAbbreviations
{
    private static readonly Dictionary<WorkLogType, string> Map = new()
    {
        [WorkLogType.RegularAttendance] = "PR",
        [WorkLogType.Overtime] = "HE",
        [WorkLogType.Absence] = "FALTA",
        [WorkLogType.Break] = "INT",
    };

    public static string Get(WorkLogType type)
    {
        return Map.TryGetValue(type, out var abbreviation)
            ? abbreviation
            : throw new DomainException($"Unknown work log type: '{type}'.");
    }
}

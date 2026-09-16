using WorkLogManager.Application.Results;

namespace WorkLogManager.Application.Interfaces;

/// <summary>
/// Renders a <see cref="MonthClosingReportData"/> into a PDF document, isolating the
/// concrete PDF library (QuestPDF) behind this port. Implemented in the
/// <c>Infrastructure</c> layer.
/// </summary>
public interface IMonthClosingReportRenderer
{
    byte[] Render(MonthClosingReportData reportData);
}

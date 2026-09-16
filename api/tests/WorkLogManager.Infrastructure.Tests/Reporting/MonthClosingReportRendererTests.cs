using System.Text;
using QuestPDF.Infrastructure;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Results;
using WorkLogManager.Infrastructure.Reporting;

namespace WorkLogManager.Infrastructure.Tests.Reporting;

/// <summary>
/// Smoke test only: the visual layout (column alignment, real page breaks with many rows,
/// font legibility) is not practically verifiable via xUnit - see the risk registered in
/// the approved development plan for the "month-closing-pdf" task. This test only asserts
/// that <see cref="MonthClosingReportRenderer.Render"/> produces a non-empty, well-formed
/// PDF byte stream.
/// </summary>
public class MonthClosingReportRendererTests
{
    static MonthClosingReportRendererTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void Render_TwoEmployees_ProducesNonEmptyPdfBytes()
    {
        var monthClosing = new MonthClosingBuilder().Build();

        var firstEmployeeDays = new List<DailyReportRow>
        {
            new(
                new DateOnly(2026, 8, 1),
                new List<WorkLogReportEntry>
                {
                    // Entries carry UTC instants (as persisted/read from the database, offset
                    // zero), the same shape MonthClosingReportRenderer receives in production -
                    // this exercises the UTC-to-business-time-zone conversion done when
                    // rendering the "Horários" cell.
                    new(
                        new DateTimeOffset(2026, 8, 1, 11, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 8, 1, 15, 0, 0, TimeSpan.Zero),
                        WorkLogType.RegularAttendance),
                },
                4 * 60 * 60,
                "Chegou atrasado"),
            new(new DateOnly(2026, 8, 2), [], 0, null),
        };

        var secondEmployeeDays = new List<DailyReportRow>
        {
            new(new DateOnly(2026, 8, 1), [], 0, null),
        };

        var reportData = new MonthClosingReportData(
            monthClosing,
            new List<EmployeeReportSection>
            {
                new(Guid.NewGuid(), "Ana Souza", "Developer", firstEmployeeDays),
                new(Guid.NewGuid(), "Bruno Lima", "QA", secondEmployeeDays),
            });

        var renderer = new MonthClosingReportRenderer();

        var bytes = renderer.Render(reportData);

        Assert.NotEmpty(bytes);

        var signature = Encoding.ASCII.GetString(bytes, 0, 5);
        Assert.Equal("%PDF-", signature);
    }

    private class MonthClosingBuilder
    {
        public MonthClosing Build()
        {
            var now = DateTimeOffset.UtcNow;
            return new MonthClosing
            {
                Id = Guid.NewGuid(),
                Month = 8,
                Year = 2026,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }
    }
}

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Results;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Infrastructure.Reporting;

/// <summary>
/// Renders the month closing attendance report as a PDF using QuestPDF (Community license -
/// see the licensing note on <c>WorkLogManager.Infrastructure.csproj</c> and
/// <c>QuestPDF.Settings.License</c> configuration in <c>Program.cs</c>). One QuestPDF
/// <c>Page()</c> is used per <see cref="EmployeeReportSection"/>, which guarantees every
/// employee always starts on a new page even if the previous employee's table did not fill
/// the last page, and lets QuestPDF automatically paginate (repeating the table header) if a
/// single employee's table does not fit on one page.
/// </summary>
public class MonthClosingReportRenderer : IMonthClosingReportRenderer
{
    private const string CompanyName = "ZANIN SOLUÇÕES METÁLICAS E COMÉRCIO";

    public byte[] Render(MonthClosingReportData reportData)
    {
        var document = Document.Create(container =>
        {
            foreach (var employee in reportData.Employees)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0.5f, Unit.Centimetre);
                    page.DefaultTextStyle(style => style.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Item().Text(CompanyName).Bold().FontSize(12);
                        column.Item().PaddingTop(5).Text(employee.EmployeeName).Bold().FontSize(11);
                        column.Item().Text(employee.EmployeeRole).FontSize(10);
                        column.Item().Text($"{reportData.MonthClosing.Month.ToString().PadLeft(2, '0')}/{reportData.MonthClosing.Year}").FontSize(10);
                    });

                    page.Content().PaddingTop(10).Table(table => BuildTable(table, employee));
                });
            }
        });

        return document.GeneratePdf();
    }

    private static void BuildTable(TableDescriptor table, EmployeeReportSection employee)
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn(0.6f); // Data
            columns.RelativeColumn(1.6f); // Horários
            columns.RelativeColumn(2f); // Observação
            columns.RelativeColumn(1f); // Assinatura
        });

        table.Header(header =>
        {
            header.Cell().Element(HeaderCellStyle).Text("Data");
            header.Cell().Element(HeaderCellStyle).Text("Horários");
            header.Cell().Element(HeaderCellStyle).Text("Observação");
            header.Cell().Element(HeaderCellStyle).Text("Assinatura");
        });

        foreach (var day in employee.Days)
        {
            table.Cell().Element(BodyCellStyle).Text(day.Date.ToString("dd/MM/yyyy"));
            table.Cell().Element(BodyCellStyle).Text(BuildScheduleText(day));
            table.Cell().Element(BodyCellStyle).Text(day.ObservationText ?? string.Empty);
            table.Cell().Element(BodyCellStyle).Text(string.Empty);
        }
    }

    private static string BuildScheduleText(DailyReportRow day)
    {
        return string.Join(
            ", ",
            day.Entries.Select(entry =>
            {
                var start = BusinessTimeZone.ToBusinessDateTime(entry.Start);
                var end = BusinessTimeZone.ToBusinessDateTime(entry.End);
                return $"{start:HH:mm}–{end:HH:mm}";
            }));
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Black)
            .Padding(2);
    }

    private static IContainer BodyCellStyle(IContainer container)
    {
        // ShowEntire() prevents a single day's row from being split across a page break
        // (e.g. one schedule line staying on the previous page and the rest moving to the
        // next one, orphaned from its date) - the whole row moves to the next page instead.
        return container
            .ShowEntire()
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(2);
    }
}

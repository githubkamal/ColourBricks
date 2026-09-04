using System.Text.Json;
using ClosedXML.Excel;
using ColourBricks.Application.Reporting.Framework;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ColourBricks.Infrastructure.Reporting.Framework;

/// <summary>P8-T07 — the ClosedXML / QuestPDF report exporter.</summary>
public sealed class ReportExporter : IReportExporter
{
    private const string IndianNumber = "#,##,##0.00";

    private static readonly JsonSerializerOptions RowJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    static ReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportExportFile Export(
        ReportResultDto result,
        ReportExportFormat format,
        IReadOnlyList<string>? visibleColumnKeys,
        string filterSummary)
    {
        IReadOnlyList<ReportColumn> columns = visibleColumnKeys is { Count: > 0 }
            ? result.Columns.Where(c => visibleColumnKeys.Contains(c.Key)).ToList()
            : result.Columns;

        // STJ polymorphism only kicks in for object-typed members, not the generic root —
        // pass the runtime type so the row's real properties are written.
        List<JsonElement> rows = result.Rows
            .Select(r => JsonSerializer.SerializeToElement(r, r.GetType(), RowJson))
            .ToList();

        return format == ReportExportFormat.Pdf
            ? new ReportExportFile($"{result.Key}.pdf", "application/pdf", ToPdf(result, columns, rows, filterSummary))
            : new ReportExportFile(
                $"{result.Key}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ToExcel(result, columns, rows, filterSummary));
    }

    private static byte[] ToExcel(
        ReportResultDto result, IReadOnlyList<ReportColumn> columns, List<JsonElement> rows, string filterSummary)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.AddWorksheet(Trim(result.Title));

        int r = 1;
        sheet.Cell(r++, 1).Value = result.Title;
        sheet.Cell(r++, 1).Value = $"Filters: {filterSummary}";
        sheet.Cell(r++, 1).Value = $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · {result.TotalCount} rows";
        r++;

        int headerRow = r;
        for (int c = 0; c < columns.Count; c++)
        {
            sheet.Cell(headerRow, c + 1).Value = columns[c].Header;
            sheet.Cell(headerRow, c + 1).Style.Font.Bold = true;
        }

        r = headerRow + 1;
        foreach (JsonElement row in rows)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                IXLCell cell = sheet.Cell(r, c + 1);
                ReportColumn column = columns[c];
                if (!row.TryGetProperty(column.Key, out JsonElement value))
                {
                    continue;
                }

                if (column.Numeric && value.ValueKind == JsonValueKind.Number)
                {
                    cell.Value = value.GetDecimal();
                    cell.Style.NumberFormat.Format = IndianNumber;
                }
                else if (value.ValueKind is JsonValueKind.Number)
                {
                    cell.Value = value.GetDecimal();
                }
                else if (value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                {
                    cell.Value = value.ToString();
                }
            }

            r++;
        }

        // Total row.
        for (int c = 0; c < columns.Count; c++)
        {
            ReportColumn column = columns[c];
            IXLCell cell = sheet.Cell(r, c + 1);
            if (c == 0)
            {
                cell.Value = "Total";
                cell.Style.Font.Bold = true;
            }

            if (column.Total is { } key && result.Totals.TryGetValue(key, out decimal total))
            {
                cell.Value = total;
                cell.Style.NumberFormat.Format = IndianNumber;
                cell.Style.Font.Bold = true;
            }
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] ToPdf(
        ReportResultDto result, IReadOnlyList<ReportColumn> columns, List<JsonElement> rows, string filterSummary)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text(result.Title).Bold().FontSize(14);
                    col.Item().Text($"Filters: {filterSummary}").FontSize(8);
                    col.Item().Text($"{result.TotalCount} rows · generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(def =>
                    {
                        foreach (ReportColumn _ in columns)
                        {
                            def.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (ReportColumn column in columns)
                        {
                            header.Cell().Element(CellStyle).Text(column.Header).Bold();
                        }
                    });

                    foreach (JsonElement row in rows)
                    {
                        foreach (ReportColumn column in columns)
                        {
                            string text = row.TryGetProperty(column.Key, out JsonElement value)
                                ? Format(value, column)
                                : "";
                            table.Cell().Element(CellStyle).Text(text);
                        }
                    }

                    foreach (ReportColumn column in columns)
                    {
                        string text = column.Total is { } key && result.Totals.TryGetValue(key, out decimal total)
                            ? total.ToString(IndianNumber, System.Globalization.CultureInfo.InvariantCulture)
                            : column == columns[0] ? "Total" : "";
                        table.Cell().Element(CellStyle).Text(text).Bold();
                    }

                    static IContainer CellStyle(IContainer c) =>
                        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(3);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static string Format(JsonElement value, ReportColumn column) => value.ValueKind switch
    {
        JsonValueKind.Number when column.Numeric =>
            value.GetDecimal().ToString(IndianNumber, System.Globalization.CultureInfo.InvariantCulture),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        _ => value.ToString(),
    };

    private static string Trim(string name) => name.Length <= 31 ? name : name[..31];
}

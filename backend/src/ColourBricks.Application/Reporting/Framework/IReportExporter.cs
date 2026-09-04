namespace ColourBricks.Application.Reporting.Framework;

public enum ReportExportFormat
{
    Xlsx,
    Pdf,
}

public sealed record ReportExportFile(string FileName, string ContentType, byte[] Content);

/// <summary>
/// P8-T07 — renders a run report to Excel or PDF. Column selection, the total row
/// and the filter summary carry through; Excel amounts are real numbers with Indian
/// grouping (<c>#,##,##0.00</c>).
/// </summary>
public interface IReportExporter
{
    ReportExportFile Export(
        ReportResultDto result,
        ReportExportFormat format,
        IReadOnlyList<string>? visibleColumnKeys,
        string filterSummary);
}

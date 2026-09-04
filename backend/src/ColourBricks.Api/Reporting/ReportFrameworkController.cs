using ColourBricks.Api.Authorization;
using ColourBricks.Application.Reporting.Framework;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Reporting;

/// <summary>
/// P8-T01 — the one endpoint every P8 report runs through. A report is a catalog
/// entry; running it applies the shared filter, scope and pagination. P8-T07 adds
/// the Excel / PDF export of the full result set.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
public sealed class ReportFrameworkController(IReportCatalog catalog, IReportExporter exporter) : ControllerBase
{
    [HttpGet("catalog")]
    [HasPermission("reports.view")]
    public ActionResult<IReadOnlyList<ReportCatalogEntryDto>> Catalog() =>
        Ok(catalog.List());

    [HttpGet("run/{key}")]
    [HasPermission("reports.view")]
    public async Task<ActionResult<ReportResultDto>> Run(
        string key, [FromQuery] ReportFilter filter, CancellationToken cancellationToken)
    {
        IReportRunner? runner = catalog.Find(key);
        if (runner is null)
        {
            return NotFound();
        }

        return Ok(await runner.RunAsync(filter, cancellationToken));
    }

    [HttpGet("export/{key}")]
    [HasPermission("reports.export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("export")]
    public async Task<IActionResult> Export(
        string key,
        [FromQuery] ReportFilter filter,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? columns = null,
        CancellationToken cancellationToken = default)
    {
        IReportRunner? runner = catalog.Find(key);
        if (runner is null)
        {
            return NotFound();
        }

        ReportResultDto result = await runner.RunAsync(filter, cancellationToken, unpaged: true);

        ReportExportFormat exportFormat = format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? ReportExportFormat.Pdf
            : ReportExportFormat.Xlsx;
        string[]? visibleColumns = columns?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        ReportExportFile file = exporter.Export(result, exportFormat, visibleColumns, Summarise(filter, result));
        return File(file.Content, file.ContentType, file.FileName);
    }

    private static string Summarise(ReportFilter filter, ReportResultDto result)
    {
        var parts = new List<string>();
        if (result.RangeFrom is { } from || result.RangeTo is { } to || filter.DatePreset != ReportDatePreset.Custom)
        {
            parts.Add($"{filter.DatePreset} ({result.RangeFrom?.ToString("yyyy-MM-dd") ?? "…"} – {result.RangeTo?.ToString("yyyy-MM-dd") ?? "…"})");
        }

        void Add(string label, long? id)
        {
            if (id is { } value)
            {
                parts.Add($"{label} #{value}");
            }
        }

        Add("Project", filter.ProjectId);
        Add("Vendor", filter.VendorId);
        Add("Subcontractor", filter.SubcontractorId);
        Add("Department", filter.DepartmentId);
        Add("Item", filter.ItemId);
        Add("Category", filter.CategoryId);
        Add("Payment mode", filter.PaymentModeId);
        Add("Account", filter.AccountId);
        if (!string.IsNullOrWhiteSpace(filter.PaymentStatus)) parts.Add($"Payment status {filter.PaymentStatus}");
        if (!string.IsNullOrWhiteSpace(filter.TransactionType)) parts.Add($"Type {filter.TransactionType}");
        if (!string.IsNullOrWhiteSpace(filter.ReconciliationStatus)) parts.Add($"Reconciliation {filter.ReconciliationStatus}");
        if (!string.IsNullOrWhiteSpace(filter.Search)) parts.Add($"Search \"{filter.Search}\"");

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }
}

namespace ColourBricks.Application.Reporting.Framework;

/// <summary>What the run endpoint returns: the page of rows plus full-set totals.</summary>
public sealed record ReportResultDto(
    string Key,
    string Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<object> Rows,
    IReadOnlyDictionary<string, decimal> Totals,
    DateOnly? RangeFrom,
    DateOnly? RangeTo,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>Catalog entry describing a report to the frontend shell.</summary>
public sealed record ReportCatalogEntryDto(
    string Key,
    string Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<string> SupportedFilters,
    /// <summary>
    /// The column keys BRD §57's "Sort" function actually resorts by (a
    /// <see cref="ReportDefinition{TRow}.SortKeys"/> key) — every report has at least
    /// one (its <c>DefaultSortBy</c>), not necessarily every column. The frontend only
    /// makes a column header clickable-to-sort when its key is in this list, so a
    /// click never shows a sort arrow that doesn't actually change the row order.
    /// </summary>
    IReadOnlyList<string> SortableColumnKeys);

/// <summary>One runnable report. The generic definition is wrapped so the API layer stays non-generic.</summary>
public interface IReportRunner
{
    string Key { get; }

    ReportCatalogEntryDto Describe();

    /// <summary>Run the report. <paramref name="unpaged"/> returns every matching row — used by export.</summary>
    Task<ReportResultDto> RunAsync(ReportFilter filter, CancellationToken cancellationToken, bool unpaged = false);
}

/// <summary>The set of registered reports.</summary>
public interface IReportCatalog
{
    IReadOnlyList<ReportCatalogEntryDto> List();

    IReportRunner? Find(string key);
}

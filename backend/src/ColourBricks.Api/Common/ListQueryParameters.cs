namespace ColourBricks.Api.Common;

/// <summary>
/// The shared query-string contract for every list endpoint (plan.md §7):
/// <c>?page=&amp;pageSize=&amp;sortBy=&amp;sortDir=&amp;search=&amp;dateFrom=&amp;dateTo=&amp;projectId=</c>.
/// Bind with <c>([FromQuery] ListQueryParameters query)</c>. The full report
/// framework in P8-T01 builds on this; it is the raw contract only.
/// </summary>
public sealed class ListQueryParameters
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;
    private readonly string _sortDir = "asc";

    /// <summary>1-based page number. Values below 1 are clamped to 1.</summary>
    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    /// <summary>Page size, clamped to [1, <see cref="MaxPageSize"/>].</summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value <= 0 ? DefaultPageSize : value, 1, MaxPageSize);
    }

    public string? SortBy { get; init; }

    /// <summary>"asc" or "desc"; anything else normalises to "asc".</summary>
    public string SortDir
    {
        get => _sortDir;
        init => _sortDir =
            string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
    }

    public string? Search { get; init; }

    public DateOnly? DateFrom { get; init; }

    public DateOnly? DateTo { get; init; }

    public long? ProjectId { get; init; }

    /// <summary>Rows to skip for the current page.</summary>
    public int Skip => (Page - 1) * PageSize;

    public int Take => PageSize;
}

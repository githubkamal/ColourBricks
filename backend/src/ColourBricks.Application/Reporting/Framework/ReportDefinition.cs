using System.Linq.Expressions;

namespace ColourBricks.Application.Reporting.Framework;

/// <summary>One display column. The frontend renders <c>row[Key]</c> under <c>Header</c>.</summary>
public sealed record ReportColumn(
    string Key,
    string Header,
    bool Numeric = false,
    bool DefaultVisible = true,
    string? Total = null,
    string? DrillThrough = null);

/// <summary>A column total, summed over the whole filtered result — never one page.</summary>
public sealed record ReportAggregate<TRow>(string Key, Expression<Func<TRow, decimal>> Selector);

/// <summary>
/// A declarative report: where its rows come from, which BRD §57 filters it honours,
/// how it sorts, and what it totals. Everything cross-cutting (date presets, project
/// scope, pagination, aggregation) is handled by the executor (P8-T01).
/// </summary>
public sealed class ReportDefinition<TRow>
    where TRow : IReportRow
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public IReadOnlyList<ReportColumn> Columns { get; init; } = [];

    public ReportFilters Supported { get; init; } = ReportFilters.All;

    /// <summary>
    /// Sort key → applier. The bool is "descending". Strongly typed so EF always
    /// translates the ordering.
    /// </summary>
    public IReadOnlyDictionary<string, Func<IQueryable<TRow>, bool, IOrderedQueryable<TRow>>> SortKeys { get; init; }
        = new Dictionary<string, Func<IQueryable<TRow>, bool, IOrderedQueryable<TRow>>>();

    public string? DefaultSortBy { get; init; }

    public bool DefaultSortDescending { get; init; }

    public IReadOnlyList<ReportAggregate<TRow>> Aggregates { get; init; } = [];
}

using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Infrastructure.Persistence;

namespace ColourBricks.Infrastructure.Reporting.Framework;

/// <summary>
/// Wraps a generic <see cref="ReportDefinition{TRow}"/> as a non-generic
/// <see cref="IReportRunner"/> so the API layer never sees the row type. Concrete
/// reports supply a definition and a source query; everything else is inherited.
/// </summary>
public abstract class ReportRunner<TRow>(ReportExecutor executor, AppDbContext db) : IReportRunner
    where TRow : IReportRow
{
    protected abstract ReportDefinition<TRow> Definition { get; }

    /// <summary>
    /// The base query. EF-backed reports return the <see cref="IQueryable{T}"/>
    /// straight away; a report computed from services does its async work first and
    /// returns <c>rows.AsQueryable()</c> — the executor handles both.
    /// </summary>
    protected abstract ValueTask<IQueryable<TRow>> SourceAsync(AppDbContext db, CancellationToken cancellationToken);

    public string Key => Definition.Key;

    public ReportCatalogEntryDto Describe() =>
        new(Definition.Key, Definition.Title, Definition.Columns, NamesOf(Definition.Supported),
            Definition.SortKeys.Keys.ToList());

    public async Task<ReportResultDto> RunAsync(ReportFilter filter, CancellationToken cancellationToken, bool unpaged = false) =>
        await executor.RunAsync(Definition, await SourceAsync(db, cancellationToken), filter, cancellationToken, unpaged);

    private static IReadOnlyList<string> NamesOf(ReportFilters supported) =>
        Enum.GetValues<ReportFilters>()
            .Where(f => f is not ReportFilters.None and not ReportFilters.All && supported.HasFlag(f))
            .Select(f => char.ToLowerInvariant(f.ToString()[0]) + f.ToString()[1..])
            .ToList();
}

/// <summary>The registered set of reports (P8-T01).</summary>
public sealed class ReportCatalog(IEnumerable<IReportRunner> runners) : IReportCatalog
{
    private readonly Dictionary<string, IReportRunner> _byKey =
        runners.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ReportCatalogEntryDto> List() =>
        _byKey.Values.Select(r => r.Describe()).OrderBy(e => e.Title).ToList();

    public IReportRunner? Find(string key) =>
        _byKey.GetValueOrDefault(key);
}

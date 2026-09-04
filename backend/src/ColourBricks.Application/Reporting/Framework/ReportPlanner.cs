using ColourBricks.Application.Auth;

namespace ColourBricks.Application.Reporting.Framework;

/// <summary>The filtered/sorted/paged query shape, before any materialisation.</summary>
public sealed class ReportPlan<TRow>
    where TRow : IReportRow
{
    /// <summary>Post-filter, post-scope, pre-sort, pre-page — the set totals are computed over.</summary>
    public required IQueryable<TRow> Filtered { get; init; }

    /// <summary>Sorted and cut to the requested page.</summary>
    public required IQueryable<TRow> Page { get; init; }

    public required DateRange ResolvedRange { get; init; }

    public required int PageNumber { get; init; }

    public required int PageSize { get; init; }
}

/// <summary>
/// Turns a <see cref="ReportDefinition{TRow}"/> + <see cref="ReportFilter"/> into a
/// <see cref="ReportPlan{TRow}"/>: resolves the date preset, applies every honoured
/// BRD §57 filter, always applies project scope, then sorts and pages. Pure
/// <see cref="IQueryable"/> composition so it runs identically over EF and in-memory.
/// </summary>
public static class ReportPlanner
{
    public const int MaxPageSize = 500;
    public const int DefaultPageSize = 50;

    public static ReportPlan<TRow> Plan<TRow>(
        IQueryable<TRow> source,
        ReportDefinition<TRow> definition,
        ReportFilter filter,
        ProjectScope scope,
        DateOnly today,
        bool unpaged = false)
        where TRow : IReportRow
    {
        DateRange range = ReportDates.Resolve(filter.DatePreset, today, filter.DateFrom, filter.DateTo);
        IQueryable<TRow> q = source;

        if (Honours(definition, ReportFilters.Date))
        {
            if (range.From is { } from)
            {
                q = q.Where(x => x.RowDate != null && x.RowDate >= from);
            }

            if (range.To is { } to)
            {
                q = q.Where(x => x.RowDate != null && x.RowDate <= to);
            }
        }

        // Project scope is unconditional — every report obeys it without opting in (BRD §64).
        if (!scope.IsUnrestricted)
        {
            List<long> allowed = scope.ProjectIds.ToList();
            q = q.Where(x => x.RowProjectId != null && allowed.Contains(x.RowProjectId.Value));
        }

        if (Honours(definition, ReportFilters.Project) && filter.ProjectId is { } projectId)
        {
            q = q.Where(x => x.RowProjectId == projectId);
        }

        if (Honours(definition, ReportFilters.Vendor) && filter.VendorId is { } vendorId)
        {
            q = q.Where(x => x.RowPartyId == vendorId);
        }

        if (Honours(definition, ReportFilters.Subcontractor) && filter.SubcontractorId is { } subId)
        {
            q = q.Where(x => x.RowPartyId == subId);
        }

        if (Honours(definition, ReportFilters.Department) && filter.DepartmentId is { } deptId)
        {
            q = q.Where(x => x.RowDepartmentId == deptId);
        }

        if (Honours(definition, ReportFilters.Item) && filter.ItemId is { } itemId)
        {
            q = q.Where(x => x.RowItemId == itemId);
        }

        if (Honours(definition, ReportFilters.Category) && filter.CategoryId is { } categoryId)
        {
            q = q.Where(x => x.RowCategoryId == categoryId);
        }

        if (Honours(definition, ReportFilters.PaymentMode) && filter.PaymentModeId is { } modeId)
        {
            q = q.Where(x => x.RowPaymentModeId == modeId);
        }

        if (Honours(definition, ReportFilters.Account) && filter.AccountId is { } accountId)
        {
            q = q.Where(x => x.RowAccountId == accountId);
        }

        if (Honours(definition, ReportFilters.PaymentStatus) && !string.IsNullOrWhiteSpace(filter.PaymentStatus))
        {
            q = q.Where(x => x.RowPaymentStatus == filter.PaymentStatus);
        }

        if (Honours(definition, ReportFilters.TransactionType) && !string.IsNullOrWhiteSpace(filter.TransactionType))
        {
            q = q.Where(x => x.RowTransactionType == filter.TransactionType);
        }

        if (Honours(definition, ReportFilters.ReconciliationStatus) && !string.IsNullOrWhiteSpace(filter.ReconciliationStatus))
        {
            q = q.Where(x => x.RowReconciliationStatus == filter.ReconciliationStatus);
        }

        if (Honours(definition, ReportFilters.Search) && !string.IsNullOrWhiteSpace(filter.Search))
        {
            string term = filter.Search.Trim();
            q = q.Where(x => x.RowSearchText != null && x.RowSearchText.Contains(term));
        }

        IQueryable<TRow> filtered = q;

        string? sortBy = !string.IsNullOrWhiteSpace(filter.SortBy) ? filter.SortBy : definition.DefaultSortBy;
        bool descending = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrWhiteSpace(filter.SortDir) && string.IsNullOrWhiteSpace(filter.SortBy) && definition.DefaultSortDescending);

        IQueryable<TRow> ordered = filtered;
        if (sortBy is not null && definition.SortKeys.TryGetValue(sortBy, out var apply))
        {
            ordered = apply(filtered, descending);
        }
        else if (definition.DefaultSortBy is { } fallback && definition.SortKeys.TryGetValue(fallback, out var applyFallback))
        {
            ordered = applyFallback(filtered, definition.DefaultSortDescending);
        }

        int page = filter.Page < 1 ? 1 : filter.Page;
        int pageSize = Math.Clamp(filter.PageSize < 1 ? DefaultPageSize : filter.PageSize, 1, MaxPageSize);

        return new ReportPlan<TRow>
        {
            Filtered = filtered,
            Page = unpaged ? ordered : ordered.Skip((page - 1) * pageSize).Take(pageSize),
            ResolvedRange = range,
            PageNumber = unpaged ? 1 : page,
            PageSize = unpaged ? int.MaxValue : pageSize,
        };
    }

    private static bool Honours<TRow>(ReportDefinition<TRow> definition, ReportFilters flag)
        where TRow : IReportRow =>
        definition.Supported.HasFlag(flag);
}

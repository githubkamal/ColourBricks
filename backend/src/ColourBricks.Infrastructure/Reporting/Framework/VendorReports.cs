using ColourBricks.Application.Allocations;
using ColourBricks.Application.Outstanding;
using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Application.VendorPayments;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting.Framework;

// ── Vendor Purchase Report (BRD §50) ────────────────────────────────────────

public sealed class VendorPurchaseReportRow : IReportRow
{
    public long Id { get; init; }
    public DateOnly Date { get; init; }
    public string Vendor { get; init; } = "";
    public string Project { get; init; } = "";
    public string Item { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal Amount { get; init; }

    public DateOnly? RowDate { get; init; }
    public long? RowProjectId { get; init; }
    public long? RowPartyId { get; init; }
    public long? RowDepartmentId => null;
    public long? RowItemId { get; init; }
    public long? RowCategoryId => null;
    public long? RowPaymentModeId => null;
    public long? RowAccountId => null;
    public string? RowPaymentStatus => null;
    public string? RowTransactionType => null;
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

public sealed class VendorPurchaseReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<VendorPurchaseReportRow>(executor, db)
{
    protected override ReportDefinition<VendorPurchaseReportRow> Definition { get; } = new()
    {
        Key = "vendor-purchase",
        Title = "Vendor Purchase Report",
        Supported = ReportFilters.Date | ReportFilters.Vendor | ReportFilters.Project
            | ReportFilters.Item | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("vendor", "Vendor"),
            new ReportColumn("project", "Project"),
            new ReportColumn("item", "Item"),
            new ReportColumn("quantity", "Qty", Numeric: true, Total: "quantity"),
            new ReportColumn("amount", "Amount", Numeric: true, Total: "amount"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = new Dictionary<string, Func<IQueryable<VendorPurchaseReportRow>, bool, IOrderedQueryable<VendorPurchaseReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
            ["amount"] = (q, d) => d ? q.OrderByDescending(x => x.Amount) : q.OrderBy(x => x.Amount),
        },
        Aggregates =
        [
            new ReportAggregate<VendorPurchaseReportRow>("amount", x => x.Amount),
            new ReportAggregate<VendorPurchaseReportRow>("quantity", x => x.Quantity),
        ],
    };

    protected override ValueTask<IQueryable<VendorPurchaseReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(
            from l in db.ObligationLines.AsNoTracking()
            join o in db.Obligations.AsNoTracking() on l.ObligationId equals o.Id
            where o.Type == ObligationType.VendorPurchase && o.Status == ObligationStatus.Active
            select new VendorPurchaseReportRow
            {
                Id = l.Id,
                Date = o.Date,
                Vendor = db.Parties.Where(p => p.Id == o.PartyId).Select(p => p.Name).FirstOrDefault() ?? "",
                Project = db.Projects.Where(p => p.Id == o.ProjectId).Select(p => p.Name).FirstOrDefault() ?? "",
                Item = l.ItemName,
                Quantity = l.Quantity,
                Amount = l.LineTotal,
                RowDate = o.Date,
                RowProjectId = o.ProjectId,
                RowPartyId = o.PartyId,
                RowItemId = l.ItemId,
                RowSearchText = l.ItemName,
            });
}

// ── Vendor Payment Report ──────────────────────────────────────────────────

public sealed class VendorPaymentReportRow : IReportRow
{
    public long Id { get; init; }
    public DateOnly Date { get; init; }
    public string Vendor { get; init; } = "";
    public string? Project { get; init; }
    public decimal Amount { get; init; }
    public string? Reference { get; init; }

    public DateOnly? RowDate { get; init; }
    public long? RowProjectId { get; init; }
    public long? RowPartyId { get; init; }
    public long? RowDepartmentId => null;
    public long? RowItemId => null;
    public long? RowCategoryId => null;
    public long? RowPaymentModeId { get; init; }
    public long? RowAccountId { get; init; }
    public string? RowPaymentStatus => null;
    public string? RowTransactionType => null;
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

public sealed class VendorPaymentReport(ReportExecutor executor, AppDbContext db)
    : ReportRunner<VendorPaymentReportRow>(executor, db)
{
    protected override ReportDefinition<VendorPaymentReportRow> Definition { get; } = new()
    {
        Key = "vendor-payment",
        Title = "Vendor Payment Report",
        Supported = ReportFilters.Date | ReportFilters.Vendor | ReportFilters.Project
            | ReportFilters.PaymentMode | ReportFilters.Account | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("vendor", "Vendor"),
            new ReportColumn("project", "Project"),
            new ReportColumn("reference", "Reference"),
            new ReportColumn("amount", "Amount", Numeric: true, Total: "amount"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = new Dictionary<string, Func<IQueryable<VendorPaymentReportRow>, bool, IOrderedQueryable<VendorPaymentReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
            ["amount"] = (q, d) => d ? q.OrderByDescending(x => x.Amount) : q.OrderBy(x => x.Amount),
        },
        Aggregates = [new ReportAggregate<VendorPaymentReportRow>("amount", x => x.Amount)],
    };

    protected override ValueTask<IQueryable<VendorPaymentReportRow>> SourceAsync(AppDbContext db, CancellationToken ct) =>
        ValueTask.FromResult(
            from s in db.Settlements.AsNoTracking()
            join party in db.Parties.AsNoTracking() on s.PartyId equals party.Id
            where s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active
                && party.Types.HasFlag(PartyType.Vendor)
            select new VendorPaymentReportRow
            {
                Id = s.Id,
                Date = s.Date,
                Vendor = party.Name,
                Project = db.Projects.Where(p => p.Id == s.ProjectId).Select(p => p.Name).FirstOrDefault(),
                Amount = s.Amount,
                Reference = s.ReferenceNo,
                RowDate = s.Date,
                RowProjectId = s.ProjectId,
                RowPartyId = s.PartyId,
                RowPaymentModeId = s.PaymentModeId,
                RowAccountId = s.AccountId,
                RowSearchText = party.Name + " " + s.ReferenceNo,
            });
}

// ── Computed vendor reports ────────────────────────────────────────────────

public sealed class VendorReportRow : IReportRow
{
    public long? Id { get; init; }
    public DateOnly? Date { get; init; }
    public string Vendor { get; init; } = "";
    public string? Project { get; init; }
    public string? Kind { get; init; }
    public string? Reference { get; init; }
    public decimal Purchase { get; init; }
    public decimal Paid { get; init; }
    public decimal Advance { get; init; }
    public decimal Outstanding { get; init; }
    public decimal RunningOutstanding { get; init; }

    public DateOnly? RowDate { get; init; }
    public long? RowProjectId { get; init; }
    public long? RowPartyId { get; init; }
    public long? RowDepartmentId => null;
    public long? RowItemId => null;
    public long? RowCategoryId => null;
    public long? RowPaymentModeId => null;
    public long? RowAccountId => null;
    public string? RowPaymentStatus => null;
    public string? RowTransactionType => null;
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

public sealed class VendorStatementReport(
    ReportExecutor executor, AppDbContext db, IVendorPaymentService payments)
    : ReportRunner<VendorReportRow>(executor, db)
{
    protected override ReportDefinition<VendorReportRow> Definition { get; } = new()
    {
        Key = "vendor-statement",
        Title = "Vendor Statement",
        Supported = ReportFilters.Date | ReportFilters.Vendor,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("kind", "Type"),
            new ReportColumn("reference", "Reference"),
            new ReportColumn("purchase", "Purchase", Numeric: true, Total: "purchase"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
            new ReportColumn("runningOutstanding", "Outstanding", Numeric: true),
        ],
        DefaultSortBy = "date",
        SortKeys = new Dictionary<string, Func<IQueryable<VendorReportRow>, bool, IOrderedQueryable<VendorReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
        },
        Aggregates =
        [
            new ReportAggregate<VendorReportRow>("purchase", x => x.Purchase),
            new ReportAggregate<VendorReportRow>("paid", x => x.Paid),
        ],
    };

    protected override async ValueTask<IQueryable<VendorReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        List<long> vendorIds = await db.Parties.AsNoTracking()
            .Where(p => p.Types.HasFlag(PartyType.Vendor)).Select(p => p.Id).ToListAsync(ct);

        var rows = new List<VendorReportRow>();
        long serial = 0;
        foreach (long vendorId in vendorIds)
        {
            string name = await db.Parties.Where(p => p.Id == vendorId).Select(p => p.Name).FirstAsync(ct);
            foreach (VendorStatementRowDto r in await payments.StatementAsync(vendorId, ct))
            {
                rows.Add(new VendorReportRow
                {
                    Id = ++serial,
                    Date = r.Date,
                    RowDate = r.Date,
                    RowPartyId = vendorId,
                    Vendor = name,
                    Kind = r.Kind,
                    Reference = r.Reference,
                    Purchase = r.PurchaseAmount,
                    Paid = r.Paid,
                    RunningOutstanding = r.RunningOutstanding,
                    Advance = r.Kind == "Advance" ? r.Paid : 0m,
                    RowSearchText = name,
                });
            }
        }

        return rows.AsQueryable();
    }
}

public sealed class VendorOutstandingReport(
    ReportExecutor executor, AppDbContext db, IOutstandingService outstanding)
    : ReportRunner<VendorReportRow>(executor, db)
{
    protected override ReportDefinition<VendorReportRow> Definition { get; } = new()
    {
        Key = "vendor-outstanding",
        Title = "Vendor Outstanding Report",
        Supported = ReportFilters.Vendor | ReportFilters.Search,
        Columns =
        [
            new ReportColumn("vendor", "Vendor"),
            new ReportColumn("purchase", "Total Purchases", Numeric: true, Total: "purchase"),
            new ReportColumn("paid", "Total Paid", Numeric: true, Total: "paid"),
            new ReportColumn("advance", "Advance", Numeric: true, Total: "advance"),
            new ReportColumn("outstanding", "Outstanding", Numeric: true, Total: "outstanding"),
        ],
        DefaultSortBy = "outstanding",
        DefaultSortDescending = true,
        SortKeys = new Dictionary<string, Func<IQueryable<VendorReportRow>, bool, IOrderedQueryable<VendorReportRow>>>
        {
            ["outstanding"] = (q, d) => d ? q.OrderByDescending(x => x.Outstanding) : q.OrderBy(x => x.Outstanding),
            ["vendor"] = (q, d) => d ? q.OrderByDescending(x => x.Vendor) : q.OrderBy(x => x.Vendor),
        },
        Aggregates =
        [
            new ReportAggregate<VendorReportRow>("purchase", x => x.Purchase),
            new ReportAggregate<VendorReportRow>("paid", x => x.Paid),
            new ReportAggregate<VendorReportRow>("advance", x => x.Advance),
            new ReportAggregate<VendorReportRow>("outstanding", x => x.Outstanding),
        ],
    };

    protected override async ValueTask<IQueryable<VendorReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var vendors = await db.Parties.AsNoTracking()
            .Where(p => p.Types.HasFlag(PartyType.Vendor))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var rows = new List<VendorReportRow>();
        foreach (var v in vendors)
        {
            VendorOutstandingSummaryDto s = await outstanding.VendorSummaryAsync(v.Id, ct);
            decimal purchases = await db.Obligations.AsNoTracking()
                .Where(o => o.Type == ObligationType.VendorPurchase && o.PartyId == v.Id
                    && o.Status == ObligationStatus.Active)
                .SumAsync(o => (decimal?)o.Amount, ct) ?? 0m;
            decimal paid = await db.Settlements.AsNoTracking()
                .Where(x => x.Direction == SettlementDirection.Out && x.PartyId == v.Id
                    && x.Status == SettlementStatus.Active)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

            rows.Add(new VendorReportRow
            {
                Id = v.Id,
                RowPartyId = v.Id,
                Vendor = v.Name,
                Purchase = purchases,
                Paid = paid,
                Advance = s.Advance,
                Outstanding = s.Total,
                RowSearchText = v.Name,
            });
        }

        return rows.AsQueryable();
    }
}

public sealed class VendorProjectWiseStatementReport(
    ReportExecutor executor, AppDbContext db, IOutstandingService outstanding)
    : ReportRunner<VendorReportRow>(executor, db)
{
    protected override ReportDefinition<VendorReportRow> Definition { get; } = new()
    {
        Key = "vendor-project-wise-statement",
        Title = "Vendor Project-wise Statement",
        Supported = ReportFilters.Vendor | ReportFilters.Project,
        Columns =
        [
            new ReportColumn("vendor", "Vendor"),
            new ReportColumn("project", "Project"),
            new ReportColumn("purchase", "Purchase", Numeric: true, Total: "purchase"),
            new ReportColumn("paid", "Paid", Numeric: true, Total: "paid"),
            new ReportColumn("outstanding", "Outstanding", Numeric: true, Total: "outstanding"),
        ],
        DefaultSortBy = "vendor",
        SortKeys = new Dictionary<string, Func<IQueryable<VendorReportRow>, bool, IOrderedQueryable<VendorReportRow>>>
        {
            ["vendor"] = (q, d) => d ? q.OrderByDescending(x => x.Vendor).ThenBy(x => x.Project)
                                     : q.OrderBy(x => x.Vendor).ThenBy(x => x.Project),
        },
        Aggregates =
        [
            new ReportAggregate<VendorReportRow>("purchase", x => x.Purchase),
            new ReportAggregate<VendorReportRow>("paid", x => x.Paid),
            new ReportAggregate<VendorReportRow>("outstanding", x => x.Outstanding),
        ],
    };

    protected override async ValueTask<IQueryable<VendorReportRow>> SourceAsync(AppDbContext db, CancellationToken ct)
    {
        var vendors = await db.Parties.AsNoTracking()
            .Where(p => p.Types.HasFlag(PartyType.Vendor))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var rows = new List<VendorReportRow>();
        foreach (var v in vendors)
        {
            IReadOnlyList<ProjectOutstandingLineDto> byProject = await outstanding.VendorByProjectAsync(v.Id, ct);

            var purchaseByProject = (await db.Obligations.AsNoTracking()
                    .Where(o => o.Type == ObligationType.VendorPurchase && o.PartyId == v.Id
                        && o.Status == ObligationStatus.Active)
                    .GroupBy(o => o.ProjectId)
                    .Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) })
                    .ToListAsync(ct))
                .ToDictionary(x => x.Key, x => x.Amount);

            var paidByProject = (await db.Settlements.AsNoTracking()
                    .Where(x => x.Direction == SettlementDirection.Out && x.PartyId == v.Id
                        && x.Status == SettlementStatus.Active && x.ProjectId != null)
                    .GroupBy(x => x.ProjectId!.Value)
                    .Select(g => new { Key = g.Key, Amount = g.Sum(x => x.Amount) })
                    .ToListAsync(ct))
                .ToDictionary(x => x.Key, x => x.Amount);

            foreach (ProjectOutstandingLineDto line in byProject)
            {
                decimal purchase = purchaseByProject.GetValueOrDefault(line.ProjectId, 0m);
                decimal paid = paidByProject.GetValueOrDefault(line.ProjectId, 0m);
                rows.Add(new VendorReportRow
                {
                    Id = line.ProjectId,
                    RowPartyId = v.Id,
                    RowProjectId = line.ProjectId,
                    Vendor = v.Name,
                    Project = line.ProjectName,
                    Purchase = purchase,
                    Paid = paid,
                    Outstanding = line.Outstanding,
                    RowSearchText = v.Name + " " + line.ProjectName,
                });
            }
        }

        return rows.AsQueryable();
    }
}

// ── Vendor Payment Allocation Report (BRD §51) ──────────────────────────────

public sealed class VendorPaymentAllocationReportRow : IReportRow
{
    public long Id { get; init; }
    public DateOnly Date { get; init; }
    public string Vendor { get; init; } = "";
    public decimal TotalPayment { get; init; }
    public string Project { get; init; } = "";
    public decimal Allocated { get; init; }
    public string Method { get; init; } = "";

    public DateOnly? RowDate { get; init; }
    public long? RowProjectId { get; init; }
    public long? RowPartyId { get; init; }
    public long? RowDepartmentId => null;
    public long? RowItemId => null;
    public long? RowCategoryId => null;
    public long? RowPaymentModeId => null;
    public long? RowAccountId => null;
    public string? RowPaymentStatus => null;
    public string? RowTransactionType => null;
    public string? RowReconciliationStatus => null;
    public string? RowSearchText { get; init; }
}

public sealed class VendorPaymentAllocationReport(
    ReportExecutor executor, AppDbContext db, IAllocationHistoryService history)
    : ReportRunner<VendorPaymentAllocationReportRow>(executor, db)
{
    protected override ReportDefinition<VendorPaymentAllocationReportRow> Definition { get; } = new()
    {
        Key = "vendor-payment-allocation",
        Title = "Vendor Payment Allocation Report",
        Supported = ReportFilters.Date | ReportFilters.Vendor | ReportFilters.Project,
        Columns =
        [
            new ReportColumn("date", "Date"),
            new ReportColumn("vendor", "Vendor"),
            new ReportColumn("totalPayment", "Total Payment", Numeric: true),
            new ReportColumn("project", "Project"),
            new ReportColumn("allocated", "Allocated", Numeric: true, Total: "allocated"),
        ],
        DefaultSortBy = "date",
        DefaultSortDescending = true,
        SortKeys = new Dictionary<string, Func<IQueryable<VendorPaymentAllocationReportRow>, bool, IOrderedQueryable<VendorPaymentAllocationReportRow>>>
        {
            ["date"] = (q, d) => d ? q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                                   : q.OrderBy(x => x.Date).ThenBy(x => x.Id),
        },
        Aggregates = [new ReportAggregate<VendorPaymentAllocationReportRow>("allocated", x => x.Allocated)],
    };

    protected override async ValueTask<IQueryable<VendorPaymentAllocationReportRow>> SourceAsync(
        AppDbContext db, CancellationToken ct)
    {
        IReadOnlyList<VendorPaymentAllocationReportRowDto> report =
            await history.ReportAsync(new VendorPaymentAllocationReportQuery(), ct);

        long serial = 0;
        return report.Select(r => new VendorPaymentAllocationReportRow
        {
            Id = ++serial,
            Date = r.Date,
            RowDate = r.Date,
            RowPartyId = r.VendorId,
            RowProjectId = r.ProjectId,
            Vendor = r.VendorName,
            TotalPayment = r.TotalPayment,
            Project = r.ProjectName,
            Allocated = r.Allocated,
            Method = r.Method,
            RowSearchText = r.VendorName + " " + r.ProjectName,
        }).AsQueryable();
    }
}

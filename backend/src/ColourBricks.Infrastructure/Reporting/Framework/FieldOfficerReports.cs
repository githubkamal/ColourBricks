using ColourBricks.Application.Outstanding;
using ColourBricks.Application.Reporting.Framework;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting.Framework;

// ── Field Officer Outstanding Report ────────────────────────────────────────
// A field officer shares the vendor payable/advance machinery wholesale (client
// request, 2026-09-04) — this mirrors VendorOutstandingReport exactly, just
// scoped to PartyType.FieldOfficer instead of PartyType.Vendor, reusing
// VendorReportRow since the shape is identical.

public sealed class FieldOfficerOutstandingReport(
    ReportExecutor executor, AppDbContext db, IOutstandingService outstanding)
    : ReportRunner<VendorReportRow>(executor, db)
{
    protected override ReportDefinition<VendorReportRow> Definition { get; } = new()
    {
        Key = "field-officer-outstanding",
        Title = "Field Officer Outstanding Report",
        Supported = ReportFilters.Search,
        Columns =
        [
            new ReportColumn("vendor", "Field Officer"),
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
        var officers = await db.Parties.AsNoTracking()
            .Where(p => p.Types.HasFlag(PartyType.FieldOfficer))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        var rows = new List<VendorReportRow>();
        foreach (var o in officers)
        {
            VendorOutstandingSummaryDto s = await outstanding.VendorSummaryAsync(o.Id, ct);
            decimal purchases = await db.Obligations.AsNoTracking()
                .Where(x => x.Type == ObligationType.VendorPurchase && x.PartyId == o.Id
                    && x.Status == ObligationStatus.Active)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            decimal paid = await db.Settlements.AsNoTracking()
                .Where(x => x.Direction == SettlementDirection.Out && x.PartyId == o.Id
                    && x.Status == SettlementStatus.Active)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

            rows.Add(new VendorReportRow
            {
                Id = o.Id,
                RowPartyId = o.Id,
                Vendor = o.Name,
                Purchase = purchases,
                Paid = paid,
                Advance = s.Advance,
                Outstanding = s.Total,
                RowSearchText = o.Name,
            });
        }

        return rows.AsQueryable();
    }
}

using ColourBricks.Application.Ledger;
using ColourBricks.Application.Outstanding;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Outstanding;

public sealed class OutstandingService(
    AppDbContext db,
    IExpenseCategoryService categories,
    TimeProvider timeProvider) : IOutstandingService
{
    public async Task<decimal> VendorTotalAsync(long vendorId, CancellationToken cancellationToken) =>
        await PayableSumAsync(cancellationToken, "vendor_payable",
            e => e.PartyId == vendorId);

    public async Task<IReadOnlyList<ProjectOutstandingLineDto>> VendorByProjectAsync(
        long vendorId, CancellationToken cancellationToken)
    {
        long category = await categories.RequireIdAsync("vendor_payable", cancellationToken);

        var rows = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.CategoryId == category && e.PartyId == vendorId && e.ProjectId != null)
            .GroupBy(e => e.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Amount = g.Sum(x => x.Credit - x.Debit) })
            .ToListAsync(cancellationToken);

        Dictionary<long, string> projectNames = await ProjectNamesAsync(
            rows.Select(r => r.ProjectId), cancellationToken);

        return rows
            .Where(r => r.Amount != 0m)
            .Select(r => new ProjectOutstandingLineDto(
                r.ProjectId, projectNames.GetValueOrDefault(r.ProjectId, ""), r.Amount))
            .OrderBy(r => r.ProjectName)
            .ToList();
    }

    public async Task<VendorOutstandingSummaryDto> VendorSummaryAsync(
        long vendorId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectOutstandingLineDto> byProject =
            await VendorByProjectAsync(vendorId, cancellationToken);
        long category = await categories.RequireIdAsync("vendor_payable", cancellationToken);

        // The project-less balance nets both ways (client request, 2026-09-04 — a field
        // officer's no-project bill posts a project-less *payable*, not just the pre-
        // existing project-less *advance* an overpayment produces). Negative = we hold
        // an advance; positive = we owe a project-less payable, which belongs in Total
        // exactly like any project's outstanding does. A vendor's project-less balance
        // could only ever be <= 0 before this (only overpayment touched it), so this is
        // purely additive for every existing vendor.
        decimal projectLessNet = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.PartyId == vendorId && e.ProjectId == null && e.CategoryId == category)
            .SumAsync(e => (decimal?)(e.Credit - e.Debit), cancellationToken) ?? 0m;
        decimal advance = projectLessNet < 0m ? -projectLessNet : 0m;
        decimal projectLessPayable = projectLessNet > 0m ? projectLessNet : 0m;

        return new VendorOutstandingSummaryDto(
            vendorId, Money.Round(byProject.Sum(l => l.Outstanding) + projectLessPayable), byProject, advance);
    }

    public async Task<decimal> SubcontractorTotalAsync(long teamId, CancellationToken cancellationToken) =>
        await PayableSumAsync(cancellationToken, "subcontractor_payable", e => e.PartyId == teamId);

    public async Task<decimal> ProjectTotalPayableAsync(long projectId, CancellationToken cancellationToken)
    {
        long[] payableCategories = await PayableCategoryIdsAsync(cancellationToken);

        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId == projectId && payableCategories.Contains(e.CategoryId))
            .SumAsync(e => (decimal?)(e.Credit - e.Debit), cancellationToken) ?? 0m;
    }

    public async Task<IReadOnlyList<PartyOutstandingLineDto>> ProjectByVendorAsync(
        long projectId, CancellationToken cancellationToken)
    {
        long category = await categories.RequireIdAsync("vendor_payable", cancellationToken);

        var rows = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.CategoryId == category && e.ProjectId == projectId && e.PartyId != null)
            .GroupBy(e => e.PartyId!.Value)
            .Select(g => new { PartyId = g.Key, Amount = g.Sum(x => x.Credit - x.Debit) })
            .ToListAsync(cancellationToken);

        List<long> partyIds = rows.Select(r => r.PartyId).ToList();
        Dictionary<long, string> names = await db.Parties.AsNoTracking()
            .Where(p => partyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return rows
            .Where(r => r.Amount != 0m)
            .Select(r => new PartyOutstandingLineDto(r.PartyId, names.GetValueOrDefault(r.PartyId, ""), r.Amount))
            .OrderBy(r => r.PartyName)
            .ToList();
    }

    public async Task<decimal> DonationOutstandingForProjectAsync(
        long projectId, CancellationToken cancellationToken) =>
        await PayableSumAsync(cancellationToken, "temple_donation_payable", e => e.ProjectId == projectId);

    public async Task<decimal> ClientOutstandingAsync(long projectId, CancellationToken cancellationToken)
    {
        decimal contractValue = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId).Select(p => p.ContractValue)
            .FirstOrDefaultAsync(cancellationToken);

        decimal received = await db.Settlements.AsNoTracking()
            .Where(s => s.ProjectId == projectId
                && s.Direction == SettlementDirection.In
                && s.Status == SettlementStatus.Active)
            .SumAsync(s => (decimal?)s.Amount, cancellationToken) ?? 0m;

        return contractValue - received;
    }

    public async Task<ProjectOutstandingSummaryDto> ProjectSummaryAsync(
        long projectId, CancellationToken cancellationToken)
    {
        decimal vendor = await PayableSumAsync(cancellationToken, "vendor_payable", e => e.ProjectId == projectId);
        decimal sub = await PayableSumAsync(cancellationToken, "subcontractor_payable", e => e.ProjectId == projectId);
        decimal custom = await PayableSumAsync(cancellationToken, "custom_work_payable", e => e.ProjectId == projectId);
        decimal donation = await DonationOutstandingForProjectAsync(projectId, cancellationToken);
        decimal client = await ClientOutstandingAsync(projectId, cancellationToken);

        return new ProjectOutstandingSummaryDto(
            projectId, vendor, sub, custom, donation, client, vendor + sub + custom + donation);
    }

    public async Task<AgeingBucketsDto> VendorAgeingAsync(long vendorId, CancellationToken cancellationToken)
    {
        List<(DateOnly Date, decimal Amount)> obligations = (await db.Obligations.AsNoTracking()
                .Where(o => o.PartyId == vendorId
                    && o.Type == ObligationType.VendorPurchase
                    && o.Status == ObligationStatus.Active)
                .Select(o => new { o.Date, o.Amount })
                .ToListAsync(cancellationToken))
            .Select(o => (o.Date, o.Amount))
            .OrderBy(o => o.Date)
            .ToList();

        decimal paid = await db.Settlements.AsNoTracking()
            .Where(s => s.PartyId == vendorId
                && s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active)
            .SumAsync(s => (decimal?)s.Amount, cancellationToken) ?? 0m;

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        decimal current = 0m, b31 = 0m, b61 = 0m, over90 = 0m;

        foreach ((DateOnly date, decimal amount) in obligations)
        {
            decimal settled = Math.Min(paid, amount);
            paid -= settled;
            decimal remaining = amount - settled;
            if (remaining <= 0m)
            {
                continue;
            }

            int days = today.DayNumber - date.DayNumber;
            if (days <= 30) current += remaining;
            else if (days <= 60) b31 += remaining;
            else if (days <= 90) b61 += remaining;
            else over90 += remaining;
        }

        return new AgeingBucketsDto(current, b31, b61, over90, current + b31 + b61 + over90);
    }

    private async Task<decimal> PayableSumAsync(
        CancellationToken cancellationToken, string categorySlug,
        System.Linq.Expressions.Expression<Func<Domain.Ledger.LedgerEntry, bool>> scope)
    {
        long category = await categories.RequireIdAsync(categorySlug, cancellationToken);
        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.CategoryId == category)
            .Where(scope)
            .SumAsync(e => (decimal?)(e.Credit - e.Debit), cancellationToken) ?? 0m;
    }

    private async Task<long[]> PayableCategoryIdsAsync(CancellationToken cancellationToken)
    {
        long[] ids =
        [
            await categories.RequireIdAsync("vendor_payable", cancellationToken),
            await categories.RequireIdAsync("subcontractor_payable", cancellationToken),
            await categories.RequireIdAsync("custom_work_payable", cancellationToken),
            await categories.RequireIdAsync("temple_donation_payable", cancellationToken),
        ];
        return ids;
    }

    private async Task<Dictionary<long, string>> ProjectNamesAsync(
        IEnumerable<long> projectIds, CancellationToken cancellationToken)
    {
        List<long> ids = projectIds.Distinct().ToList();
        return await db.Projects.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
    }
}

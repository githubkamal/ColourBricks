using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using ColourBricks.Application.Budgets;
using ColourBricks.Application.Ledger;
using ColourBricks.Application.Outstanding;
using ColourBricks.Application.Reporting;
using ColourBricks.Domain.Projects;
using ColourBricks.Domain.Services;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Reporting;

/// <summary>P5-T02..T06 — read-only budget / ledger / P&L / dashboard reporting.</summary>
public sealed class ReportingService(
    AppDbContext db,
    ILedgerQueryService ledger,
    IProjectBudgetService budgets,
    IOutstandingService outstanding,
    IProjectScopeFilter scopeFilter) : IReportingService
{
    private static readonly string[] DashboardBuckets =
    [
        "Building Construction / Mesthri", "Interior", "Plumbing", "Electrical", "Materials",
        "Vendors", "Customized Work", "Temple Donations", "Personal/Common Expenses",
        "Office/Common Expenses", "Savings Allocation", "Loan/EMI", "Other Expenses",
    ];

    // ── P5-T02 ───────────────────────────────────────────────────────────────

    public async Task<BudgetVsActualDto> BudgetVsActualAsync(long projectId, CancellationToken ct)
    {
        await GuardScopeAsync(projectId, ct);
        Project project = await db.Projects.AsNoTracking().FirstAsync(p => p.Id == projectId, ct);

        ProjectBudgetDto? budget = await budgets.GetCurrentAsync(projectId, ct);
        decimal threshold = budget?.ApproachingThresholdPercent ?? 90m;
        Dictionary<long, decimal> budgetByCat = budget?.Lines.ToDictionary(l => l.CategoryId, l => l.Amount) ?? [];

        IReadOnlyDictionary<long, decimal> actualByCat = await ledger.GetProjectCostByCategoryAsync(projectId, ct);

        List<long> categoryIds = budgetByCat.Keys.Union(actualByCat.Keys).Distinct().ToList();
        Dictionary<long, (string Name, string Bucket)> names = await db.ExpenseCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.Name, c.Bucket), ct);

        var rows = categoryIds
            .Select(id =>
            {
                decimal b = budgetByCat.GetValueOrDefault(id, 0m);
                decimal a = actualByCat.GetValueOrDefault(id, 0m);
                decimal variance = Money.Round(b - a); // positive = under budget
                decimal? pct = b == 0m ? null : Money.Round((a - b) / b * 100m);
                string status = a > b
                    ? "Exceeded"
                    : b > 0m && a >= b * threshold / 100m ? "Approaching" : "Within";
                return new BudgetVsActualRowDto(
                    id, names.GetValueOrDefault(id).Name ?? "", names.GetValueOrDefault(id).Bucket ?? "",
                    b, a, variance, pct, status);
            })
            .OrderBy(r => r.CategoryName)
            .ToList();

        decimal actualCost = await ledger.GetProjectActualCostAsync(projectId, ct);
        decimal overrun = Money.Round(actualCost - project.EstimatedCost);
        string message = overrun > 0m
            ? $"Budget Exceeded by {overrun:0.00}"
            : overrun < 0m ? $"Within budget by {-overrun:0.00}" : "On budget";

        return new BudgetVsActualDto(
            projectId, budget?.RevisionNumber, project.EstimatedCost, actualCost, threshold, message, rows);
    }

    // ── P5-T03 ───────────────────────────────────────────────────────────────

    public async Task<ProjectLedgerViewDto> ProjectLedgerAsync(
        long projectId, DateOnly? from, DateOnly? to, long? categoryId, long? partyId, CancellationToken ct)
    {
        await GuardScopeAsync(projectId, ct);

        var all = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Join(db.ExpenseCategories.AsNoTracking(), e => e.CategoryId, c => c.Id,
                (e, c) => new
                {
                    e.Id, e.EntryDate, e.CategoryId, c.Name, c.Bucket, c.IsCost,
                    e.PartyId, e.Debit, e.Credit, e.SourceType, e.SourceId, e.IsReversal,
                })
            .Where(x => x.IsCost || x.Bucket == "Income")
            .OrderBy(x => x.EntryDate).ThenBy(x => x.Id)
            .ToListAsync(ct);

        List<long> partyIds = all.Where(x => x.PartyId != null).Select(x => x.PartyId!.Value).Distinct().ToList();
        Dictionary<long, string> partyNames = await db.Parties.AsNoTracking()
            .Where(p => partyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        decimal opening = 0m, running = 0m;
        var lines = new List<ProjectLedgerLineDto>();
        foreach (var x in all)
        {
            bool income = x.Bucket == "Income";
            decimal credit = income ? x.Debit : x.Credit;
            decimal debit = income ? x.Credit : x.Debit;
            decimal delta = credit - debit;

            if (from is { } f && x.EntryDate < f)
            {
                opening += delta;
                running += delta;
                continue;
            }

            if ((to is { } t && x.EntryDate > t)
                || (categoryId is { } cid && x.CategoryId != cid)
                || (partyId is { } pid && x.PartyId != pid))
            {
                continue;
            }

            running += delta;
            string? party = x.PartyId is { } p ? partyNames.GetValueOrDefault(p) : null;
            string description = $"{Friendly(x.SourceType)} — {x.Name}"
                + (party is null ? "" : $" — {party}")
                + (x.IsReversal ? " (reversal)" : "");

            lines.Add(new ProjectLedgerLineDto(
                x.Id, x.EntryDate, description, Money.Round(credit), Money.Round(debit),
                Money.Round(running), x.SourceType, x.SourceId, x.IsReversal,
                x.CategoryId, x.Name, x.PartyId, party));
        }

        decimal closing = lines.Count == 0 ? Money.Round(opening) : lines[^1].RunningBalance;
        return new ProjectLedgerViewDto(projectId, Money.Round(opening), closing, lines);
    }

    // ── P5-T04 ───────────────────────────────────────────────────────────────

    public async Task<ProjectPnlDto> ProjectPnlAsync(long projectId, string revenueBasis, CancellationToken ct)
    {
        await GuardScopeAsync(projectId, ct);
        Project project = await db.Projects.AsNoTracking().FirstAsync(p => p.Id == projectId, ct);
        return await BuildPnlAsync(project, revenueBasis, ct);
    }

    public async Task<CompanyPnlDto> CompanyPnlAsync(string revenueBasis, CancellationToken ct)
    {
        ProjectScope scope = await scopeFilter.GetScopeAsync(ct);
        List<Project> projects = await db.Projects.AsNoTracking()
            .Where(p => scope.IsUnrestricted || scope.ProjectIds.Contains(p.Id))
            .ToListAsync(ct);

        var rows = new List<ProjectPnlDto>();
        foreach (Project p in projects)
        {
            rows.Add(await BuildPnlAsync(p, revenueBasis, ct));
        }

        decimal revenue = rows.Sum(r => r.Revenue);
        decimal estimated = rows.Sum(r => r.EstimatedCost);
        // Company-wide actual cost must include company-level Personal/Office/Custom
        // spend that hasn't been allocated to a project yet, or it silently understates
        // true spend versus the dashboard's "Overall Expenses" tile (client request,
        // 2026-09-04 — product validation found these two figures used to disagree).
        decimal unallocated = await CompanyWideUnallocatedExpenseAsync(ct);
        decimal actual = rows.Sum(r => r.ActualCost) + unallocated;
        decimal gross = revenue - actual;
        return new CompanyPnlDto(
            Normalise(revenueBasis), Money.Round(revenue), Money.Round(estimated), Money.Round(actual),
            Money.Round(gross), Pct(gross, revenue), rows.OrderBy(r => r.ProjectName).ToList(),
            Money.Round(unallocated));
    }

    private async Task<ProjectPnlDto> BuildPnlAsync(Project project, string revenueBasis, CancellationToken ct)
    {
        string basis = Normalise(revenueBasis);
        decimal revenue = basis == "Receipts"
            ? await ProjectReceiptsAsync(project.Id, ct)
            : project.ContractValue;
        decimal actualCost = await ledger.GetProjectActualCostAsync(project.Id, ct);
        decimal gross = Money.Round(revenue - actualCost);
        return new ProjectPnlDto(
            project.Id, project.Name, basis, Money.Round(revenue), project.EstimatedCost,
            Money.Round(actualCost), gross, Pct(gross, revenue),
            Money.Round(actualCost - project.EstimatedCost));
    }

    // ── P5-T05 ───────────────────────────────────────────────────────────────

    public async Task<ProjectDashboardDto> ProjectDashboardAsync(long projectId, CancellationToken ct)
    {
        await GuardScopeAsync(projectId, ct);
        Project project = await db.Projects.AsNoTracking().FirstAsync(p => p.Id == projectId, ct);

        decimal actualCost = await ledger.GetProjectActualCostAsync(projectId, ct);
        decimal income = await ledger.GetProjectIncomeAsync(projectId, ct);
        decimal paid = await ProjectPaidAsync(projectId, ct);
        decimal outstandingTotal = (await outstanding.ProjectSummaryAsync(projectId, ct)).TotalPayable;
        ProjectBudgetDto? budget = await budgets.GetCurrentAsync(projectId, ct);
        decimal budgetBasis = budget is { BudgetTotal: > 0m } ? budget.BudgetTotal : project.EstimatedCost;

        decimal expectedProfit = project.ContractValue - project.EstimatedCost;
        decimal actualProfit = income - actualCost;
        decimal currentBalance = income - paid;

        var summary = new List<DashboardTileDto>
        {
            Tile("projectValue", "Project Value", project.ContractValue, null),
            Tile("estimatedCost", "Estimated Cost", project.EstimatedCost, null),
            Tile("actualCost", "Actual Cost", actualCost, $"/api/v1/projects/{projectId}/ledger"),
            Tile("totalIncome", "Total Income", income, $"/api/v1/projects/{projectId}/receipts"),
            Tile("totalExpenses", "Total Expenses", actualCost, $"/api/v1/projects/{projectId}/ledger"),
            Tile("totalPaid", "Total Paid", paid, null),
            Tile("totalOutstanding", "Total Outstanding", outstandingTotal, $"/api/v1/projects/{projectId}/outstanding-by-vendor"),
            Tile("currentBalance", "Current Balance", currentBalance, null),
            Tile("expectedProfit", "Expected Profit", expectedProfit, null),
            Tile("actualProfit", "Actual Profit", actualProfit, $"/api/v1/projects/{projectId}/pnl"),
            Tile("profitPercent", "Profit %", Pct(actualProfit, income), null),
            Tile("budgetUtilisationPercent", "Budget Utilisation %",
                budgetBasis == 0m ? 0m : Money.Round(actualCost / budgetBasis * 100m), null),
        };

        IReadOnlyDictionary<string, decimal> byBucket = await ledger.GetProjectCostByBucketAsync(projectId, ct);
        var breakdown = DashboardBuckets
            .Select(b => new ExpenseBreakdownRowDto(b, Money.Round(byBucket.GetValueOrDefault(b, 0m))))
            .ToList();

        return new ProjectDashboardDto(
            projectId, project.Name, summary, breakdown, Money.Round(breakdown.Sum(r => r.Amount)));
    }

    // ── P5-T06 ───────────────────────────────────────────────────────────────

    public async Task<CompanyDashboardDto> CompanyDashboardAsync(CancellationToken ct)
    {
        ProjectScope scope = await scopeFilter.GetScopeAsync(ct);
        List<Project> projects = await db.Projects.AsNoTracking()
            .Where(p => scope.IsUnrestricted || scope.ProjectIds.Contains(p.Id))
            .ToListAsync(ct);
        List<long> projectIds = projects.Select(p => p.Id).ToList();

        decimal income = 0m, actualCost = 0m;
        var profitRows = new List<CompanyProjectProfitRowDto>();
        foreach (Project p in projects)
        {
            decimal pi = await ledger.GetProjectIncomeAsync(p.Id, ct);
            decimal pc = await ledger.GetProjectActualCostAsync(p.Id, ct);
            income += pi;
            actualCost += pc;
            profitRows.Add(new CompanyProjectProfitRowDto(
                p.Id, p.Name, Money.Round(pi), Money.Round(pc), Money.Round(pi - pc)));
        }

        decimal cashBank = scope.IsUnrestricted
            ? await SumAccountBalancesAsync(ct)
            : 0m;

        int ongoing = projects.Count(p => p.Status == ProjectStatus.Ongoing);
        int completed = projects.Count(p => p.Status == ProjectStatus.Completed);

        decimal vendorOut = await PayableAcrossProjectsAsync("vendor_payable", projectIds, ct);
        decimal subOut = await PayableAcrossProjectsAsync("subcontractor_payable", projectIds, ct);

        int pendingRecon = await db.BankTransactions.CountAsync(
            t => t.Status == Domain.Banking.BankTransactionStatus.Pending, ct);

        // Company-level Personal/Office/Custom expenses, unallocated to any project (BRD
        // §44) — same helper CompanyPnlAsync uses, so these two "total expenses" figures
        // can never diverge again (client request, 2026-09-04 — product validation found
        // they previously did: this used to omit Custom entirely and CompanyPnlAsync
        // didn't include this bucket at all).
        decimal companyExpense = await CompanyWideUnallocatedExpenseAsync(ct);
        decimal savings = await CompanyLevelCostAsync(new List<string> { "savings_allocation" }, ct);
        decimal totalExpense = actualCost + companyExpense;

        var tiles = new List<DashboardTileDto>
        {
            Tile("income", "Overall Income", Money.Round(income), null),
            Tile("expenses", "Overall Expenses", Money.Round(totalExpense), null),
            Tile("profitLoss", "Profit / Loss", Money.Round(income - totalExpense), null),
            Tile("savings", "Savings", Money.Round(savings), "/api/v1/common-expenses?type=Savings"),
            Tile("ongoingProjects", "Ongoing Projects", ongoing, null),
            Tile("completedProjects", "Completed Projects", completed, null),
            Tile("cashBankPosition", "Cash & Bank Position", Money.Round(cashBank), "/api/v1/accounts"),
            Tile("vendorOutstanding", "Vendor Outstanding", Money.Round(vendorOut), null),
            Tile("subcontractorOutstanding", "Subcontractor Outstanding", Money.Round(subOut), null),
            Tile("pendingReconciliation", "Pending Reconciliation", pendingRecon, "/api/v1/reconciliation?status=Pending"),
        };

        List<MonthlyFlowDto> monthly = await MonthlyFlowAsync(projectIds, ct);

        return new CompanyDashboardDto(tiles, profitRows.OrderBy(r => r.ProjectName).ToList(), monthly);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task GuardScopeAsync(long projectId, CancellationToken ct)
    {
        ProjectScope scope = await scopeFilter.GetScopeAsync(ct);
        if (!scope.Allows(projectId))
        {
            throw new ProjectAccessDeniedException(projectId);
        }
    }

    private async Task<decimal> ProjectReceiptsAsync(long projectId, CancellationToken ct) =>
        await db.Settlements.AsNoTracking()
            .Where(s => s.ProjectId == projectId
                && s.Direction == SettlementDirection.In
                && s.Status == SettlementStatus.Active)
            .SumAsync(s => (decimal?)s.Amount, ct) ?? 0m;

    private async Task<decimal> ProjectPaidAsync(long projectId, CancellationToken ct)
    {
        decimal direct = await db.Settlements.AsNoTracking()
            .Where(s => s.ProjectId == projectId
                && s.Direction == SettlementDirection.Out
                && s.Status == SettlementStatus.Active)
            .SumAsync(s => (decimal?)s.Amount, ct) ?? 0m;

        decimal allocated = await db.Allocations.AsNoTracking()
            .Where(a => a.ProjectId == projectId
                && (a.SettlementId == null
                    || db.Settlements.Any(s => s.Id == a.SettlementId && s.Status == SettlementStatus.Active)))
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

        return direct + allocated;
    }

    private async Task<decimal> SumAccountBalancesAsync(CancellationToken ct)
    {
        List<long> accountIds = await db.Accounts.AsNoTracking().Select(a => a.Id).ToListAsync(ct);
        decimal total = 0m;
        foreach (long id in accountIds)
        {
            total += await ledger.GetAccountBalanceAsync(id, ct);
        }

        return total;
    }

    /// <summary>
    /// Personal/Office/Custom common expenses not yet allocated to a project (BRD §44).
    /// Savings is tracked separately and deliberately excluded — it's not a P&amp;L expense.
    /// Shared by <see cref="CompanyPnlAsync"/> and <see cref="CompanyDashboardAsync"/> so
    /// "total company expenses" is computed exactly once, not twice in a way that can
    /// silently drift apart (client request, 2026-09-04).
    /// </summary>
    private Task<decimal> CompanyWideUnallocatedExpenseAsync(CancellationToken ct) =>
        CompanyLevelCostAsync(["personal_common", "office_common", "custom_common"], ct);

    private async Task<decimal> CompanyLevelCostAsync(List<string> slugs, CancellationToken ct)
    {
        List<long> categoryIds = await db.ExpenseCategories.AsNoTracking()
            .Where(c => slugs.Contains(c.Slug)).Select(c => c.Id).ToListAsync(ct);
        // Only the expense leg — the mirrored account-scoped leg (AccountId set) is the cash movement.
        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId == null && e.AccountId == null && categoryIds.Contains(e.CategoryId))
            .SumAsync(e => (decimal?)(e.Debit - e.Credit), ct) ?? 0m;
    }

    private async Task<decimal> PayableAcrossProjectsAsync(string slug, List<long> projectIds, CancellationToken ct)
    {
        long category = await db.ExpenseCategories.AsNoTracking()
            .Where(c => c.Slug == slug).Select(c => c.Id).FirstAsync(ct);
        return await db.LedgerEntries.AsNoTracking()
            .Where(e => e.CategoryId == category && e.ProjectId != null && projectIds.Contains(e.ProjectId.Value))
            .SumAsync(e => (decimal?)(e.Credit - e.Debit), ct) ?? 0m;
    }

    private async Task<List<MonthlyFlowDto>> MonthlyFlowAsync(List<long> projectIds, CancellationToken ct)
    {
        var rows = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ProjectId != null && projectIds.Contains(e.ProjectId.Value))
            .Join(db.ExpenseCategories.AsNoTracking(), e => e.CategoryId, c => c.Id,
                (e, c) => new { e.EntryDate, c.Bucket, c.IsCost, e.Debit, e.Credit })
            .Where(x => x.IsCost || x.Bucket == "Income")
            .ToListAsync(ct);

        return rows
            .GroupBy(x => new { x.EntryDate.Year, x.EntryDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyFlowDto(
                $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                Money.Round(g.Where(x => x.Bucket == "Income").Sum(x => x.Debit - x.Credit)),
                Money.Round(g.Where(x => x.IsCost).Sum(x => x.Debit - x.Credit))))
            .ToList();
    }

    private static DashboardTileDto Tile(string key, string label, decimal value, string? drill) =>
        new(key, label, Money.Round(value), drill);

    private static decimal Pct(decimal part, decimal whole) =>
        whole == 0m ? 0m : Money.Round(part / whole * 100m);

    private static string Normalise(string basis) =>
        string.Equals(basis, "Receipts", StringComparison.OrdinalIgnoreCase) ? "Receipts" : "Contract";

    private static string Friendly(string sourceType) => sourceType switch
    {
        "ProjectReceipt" => "Client receipt",
        "VendorPurchase" => "Vendor purchase",
        "VendorPayment" => "Vendor payment",
        "DirectExpense" => "Direct expense",
        "Labour" => "Labour",
        "DonationPayment" => "Temple donation",
        "CustomWork" => "Customised work",
        "InternalTransfer" => "Internal transfer",
        _ => sourceType,
    };
}

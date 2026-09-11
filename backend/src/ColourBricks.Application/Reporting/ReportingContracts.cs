namespace ColourBricks.Application.Reporting;

// ── P5-T02 budget vs actual ─────────────────────────────────────────────────

public sealed record BudgetVsActualRowDto(
    long CategoryId,
    string CategoryName,
    string Bucket,
    decimal Budget,
    decimal Actual,
    decimal Variance,
    decimal? VariancePercent,
    string Status);          // "Within" | "Approaching" | "Exceeded"

public sealed record BudgetVsActualDto(
    long ProjectId,
    int? BudgetRevisionNumber,
    decimal EstimatedCost,
    decimal ActualCost,
    decimal ApproachingThresholdPercent,
    string OverrunMessage,   // BRD §40 format
    IReadOnlyList<BudgetVsActualRowDto> Rows);

// ── P5-T03 project financial ledger ────────────────────────────────────────

public sealed record ProjectLedgerLineDto(
    long EntryId,
    DateOnly Date,
    string Description,
    decimal Credit,
    decimal Debit,
    decimal RunningBalance,
    string SourceType,
    long SourceId,
    bool IsReversal,
    long CategoryId,
    string CategoryName,
    long? PartyId,
    string? PartyName,
    /// <summary>
    /// The category's accounting bucket ("Cost" | "Income" | "Liability" | "Asset").
    /// A "Liability" row (e.g. a vendor payable created alongside a purchase) hasn't
    /// moved any cash yet — it nets to zero against its own purchase-time leg — so the
    /// UI should label it distinctly from a real cash Credit/Debit.
    /// </summary>
    string Bucket);

public sealed record ProjectLedgerViewDto(
    long ProjectId,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalCredit,
    decimal TotalDebit,
    IReadOnlyList<ProjectLedgerLineDto> Lines);

// ── P5-T04 profit and loss ─────────────────────────────────────────────────

public sealed record ProjectPnlDto(
    long ProjectId,
    string ProjectName,
    string RevenueBasis,     // "Contract" | "Receipts"
    decimal Revenue,
    decimal EstimatedCost,
    decimal ActualCost,
    decimal GrossProfit,
    decimal ProfitPercent,
    decimal BudgetVariance);

public sealed record CompanyPnlDto(
    string RevenueBasis,
    decimal Revenue,
    decimal EstimatedCost,
    decimal ActualCost,
    decimal GrossProfit,
    decimal ProfitPercent,
    IReadOnlyList<ProjectPnlDto> Projects,
    /// <summary>
    /// Personal/Office/Custom common expenses not yet moved onto any project by an
    /// allocation run (BRD §44) — already included in <see cref="ActualCost"/>, shown
    /// separately so it's clear why company ActualCost exceeds the sum of the
    /// projects' own ActualCost (client request, 2026-09-04 — product validation
    /// found this bucket used to be silently excluded from company P&amp;L entirely).
    /// </summary>
    decimal UnallocatedExpense = 0m);

// ── P5-T05 project dashboard ───────────────────────────────────────────────

public sealed record DashboardTileDto(string Key, string Label, decimal Value, string? DrillUrl);

public sealed record ExpenseBreakdownRowDto(string Bucket, decimal Amount);

public sealed record ProjectDashboardDto(
    long ProjectId,
    string ProjectName,
    IReadOnlyList<DashboardTileDto> Summary,
    IReadOnlyList<ExpenseBreakdownRowDto> ExpenseBreakdown,
    decimal TotalExpenses,
    /// <summary>Month-by-month income vs expense for this project (client request,
    /// 2026-09-07) — same shape as the company dashboard's trend chart.</summary>
    IReadOnlyList<MonthlyFlowDto> MonthlyFlow);

// ── P5-T06 company dashboard ───────────────────────────────────────────────

public sealed record CompanyProjectProfitRowDto(
    long ProjectId, string ProjectName, decimal Revenue, decimal ActualCost, decimal Profit);

public sealed record MonthlyFlowDto(string Month, decimal Income, decimal Expense);

public sealed record CompanyDashboardDto(
    IReadOnlyList<DashboardTileDto> Tiles,
    IReadOnlyList<CompanyProjectProfitRowDto> ProjectProfitability,
    IReadOnlyList<MonthlyFlowDto> MonthlyFlow);

public sealed class ProjectAccessDeniedException(long projectId)
    : Exception($"Project #{projectId} is outside your access.");

public interface IReportingService
{
    Task<BudgetVsActualDto> BudgetVsActualAsync(long projectId, CancellationToken cancellationToken);

    Task<ProjectLedgerViewDto> ProjectLedgerAsync(
        long projectId, DateOnly? from, DateOnly? to, long? categoryId, long? partyId,
        CancellationToken cancellationToken);

    Task<ProjectPnlDto> ProjectPnlAsync(long projectId, string revenueBasis, CancellationToken cancellationToken);

    Task<CompanyPnlDto> CompanyPnlAsync(string revenueBasis, CancellationToken cancellationToken);

    Task<ProjectDashboardDto> ProjectDashboardAsync(long projectId, CancellationToken cancellationToken);

    /// <summary>
    /// <paramref name="period"/> is one of "Weekly", "Monthly" (default), "Yearly",
    /// "Entire" (all time) or "Custom" — scopes the dashboard's flow figures (income,
    /// expenses, profit, project profitability, the trend chart). Point-in-time
    /// balances elsewhere on the dashboard are never date-scoped. When
    /// <paramref name="period"/> is "Custom", <paramref name="dateFrom"/> and
    /// <paramref name="dateTo"/> are used directly instead of being derived from the
    /// period name (client request, 2026-09-07); they're ignored for every other
    /// period value.
    /// </summary>
    Task<CompanyDashboardDto> CompanyDashboardAsync(
        string period, DateOnly? dateFrom, DateOnly? dateTo, CancellationToken cancellationToken);
}

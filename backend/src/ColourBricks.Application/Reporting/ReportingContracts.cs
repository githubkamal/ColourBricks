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
    string? PartyName);

public sealed record ProjectLedgerViewDto(
    long ProjectId,
    decimal OpeningBalance,
    decimal ClosingBalance,
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
    decimal TotalExpenses);

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

    Task<CompanyDashboardDto> CompanyDashboardAsync(CancellationToken cancellationToken);
}

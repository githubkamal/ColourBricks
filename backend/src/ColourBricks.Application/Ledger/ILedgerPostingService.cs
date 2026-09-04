namespace ColourBricks.Application.Ledger;

/// <summary>
/// The only way anything writes to the financial ledger (plan.md §5.2). Every
/// posting is balanced and carries a source trace; reversals mirror, never delete
/// (plan.md §5.6).
/// </summary>
public interface ILedgerPostingService
{
    /// <summary>Writes the posting's legs as <c>LedgerEntry</c> rows in one transaction.</summary>
    Task PostAsync(LedgerPosting posting, CancellationToken cancellationToken);

    /// <summary>
    /// Writes mirrored (debit/credit swapped) entries for every non-reversal entry of
    /// the source, tagged <c>IsReversal = true</c>. Throws
    /// <see cref="LedgerAlreadyReversedException"/> if already reversed and
    /// <see cref="LedgerSourceNotFoundException"/> if nothing is posted.
    /// </summary>
    Task ReverseAsync(string sourceType, long sourceId, string reason, CancellationToken cancellationToken);
}

public interface ILedgerQueryService
{
    /// <summary>Opening balance + Σcredit − Σdebit for an account (plan.md §5.3).</summary>
    Task<decimal> GetAccountBalanceAsync(long accountId, CancellationToken cancellationToken);

    /// <summary>Σ(debit − credit) per expense bucket for a project (BRD §5 breakdown).</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetProjectCostByBucketAsync(
        long projectId, CancellationToken cancellationToken);

    /// <summary>Total project cost = Σ(debit − credit) over cost categories.</summary>
    Task<decimal> GetProjectActualCostAsync(long projectId, CancellationToken cancellationToken);

    /// <summary>Σ(debit − credit) per expense category id for a project, cost categories only (BRD §41).</summary>
    Task<IReadOnlyDictionary<long, decimal>> GetProjectCostByCategoryAsync(
        long projectId, CancellationToken cancellationToken);

    /// <summary>Total project income = Σ(debit − credit) over non-cost (income) categories (plan.md §5.3).</summary>
    Task<decimal> GetProjectIncomeAsync(long projectId, CancellationToken cancellationToken);

    /// <summary>
    /// A party's outstanding under a payable category = Σ(credit − debit) — obligation
    /// credits raise it, settlement debits lower it (plan.md §5.3).
    /// </summary>
    Task<decimal> GetPartyPayableBalanceAsync(
        long partyId, long categoryId, CancellationToken cancellationToken);

    /// <summary>As <see cref="GetPartyPayableBalanceAsync"/> but scoped to one project.</summary>
    Task<decimal> GetPartyPayableBalanceForProjectAsync(
        long partyId, long projectId, long categoryId, CancellationToken cancellationToken);

    /// <summary>
    /// A vendor's advance / credit balance (BRD §25, P3-T05): the negative of the
    /// project-less payable held at the party. An over-payment debits payable with no
    /// project, so Σ(credit − debit) there goes negative; this returns it as a
    /// positive advance, floored at zero.
    /// </summary>
    Task<decimal> GetVendorAdvanceBalanceAsync(
        long partyId, long categoryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectLedgerRowDto>> GetProjectLedgerAsync(
        long projectId, CancellationToken cancellationToken);
}

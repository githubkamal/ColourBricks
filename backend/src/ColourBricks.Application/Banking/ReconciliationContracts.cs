namespace ColourBricks.Application.Banking;

// ── match suggestions (P4-T04) ────────────────────────────────────────────────

public sealed record MatchSuggestionDto(
    long SettlementId,
    long? PartyId,
    string PartyName,
    DateOnly Date,
    decimal Amount,
    string? ReferenceNo,
    string Description,
    int Score,
    IReadOnlyList<string> Reasons);

public sealed record MatchSuggestionsDto(
    long BankTransactionId,
    int AutoSelectThreshold,
    long? AutoSelectSettlementId,
    IReadOnlyList<MatchSuggestionDto> Suggestions);

public interface IMatchSuggestionService
{
    Task<MatchSuggestionsDto> SuggestAsync(long bankTransactionId, CancellationToken cancellationToken);

    /// <summary>Remember the distinctive tokens of a bank narration as aliases of a party (BRD §32).</summary>
    Task RememberAliasesAsync(long partyId, string narration, CancellationToken cancellationToken);
}

// ── reconciliation queue (P4-T05) ─────────────────────────────────────────────

public sealed record ReconciliationRowDto(
    long Id,
    DateOnly Date,
    string Bank,
    string Description,
    string Type,               // "Credit" | "Debit"
    decimal Credit,
    decimal Debit,
    string? Counterparty,      // Client / Vendor name (best guess from hint/link)
    string Projects,           // "Project A" | "3 Projects" | ""
    decimal Allocated,
    decimal Difference,
    string Status,
    IReadOnlyList<ProjectAllocationDto> ProjectDetail);

public sealed record ReconciliationQueueQuery(
    long? AccountId = null,
    string? Status = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    decimal? AmountMin = null,
    decimal? AmountMax = null,
    bool? Matched = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 100);

public sealed record ReconciliationQueueDto(
    IReadOnlyList<ReconciliationRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record BulkExcludeRequest(IReadOnlyList<long> Ids, string Reason);

// ── credit / debit reconciliation (P4-T06, P4-T07) ────────────────────────────

public sealed record ReconcileCreditRequest(
    long ClientId,
    long ProjectId,                 // exactly one — BRD §34 / rule 46
    string IncomeType = "ClientAdvance",
    long? ExistingReceiptId = null);

public sealed record DebitAllocationInput(long ProjectId, decimal Amount);

public sealed record ReconcileDebitRequest(
    long? VendorId = null,
    IReadOnlyList<DebitAllocationInput>? Allocations = null,
    long? ExistingPaymentId = null);

public sealed record ReconcileResultDto(
    long BankTransactionId,
    long SettlementId,
    bool SettlementCreated,
    string Status);

public sealed record ReconciliationProposalDto(
    long BankTransactionId,
    string Type,
    decimal Amount,
    IReadOnlyList<ProjectAllocationDto> Hints,
    IReadOnlyList<ProjectAllocationDto> FifoProposal);

public sealed record UnreconcileRequest(string Reason);

// ── debit map/split (client request, 2026-09-04) ─────────────────────────────

/// <summary>What one line of a debit split maps to.</summary>
public enum ReconciliationAllocationTarget
{
    /// <summary>A vendor debit — <see cref="ReconciliationAllocationInput.VendorId"/> is required;
    /// <see cref="ReconciliationAllocationInput.ProjectId"/> is optional (no project = a vendor advance).</summary>
    Vendor = 1,
    Personal = 2,
    Office = 3,
    Savings = 4,

    /// <summary>A catch-all bucket alongside Personal/Office/Savings (client request, 2026-09-04).</summary>
    Custom = 5,

    /// <summary>
    /// Money given to (or a bill settled with) a field officer — same shape as
    /// <see cref="Vendor"/>, posted through the same party payable/advance path;
    /// kept as a distinct label for clarity, not a different code path (client
    /// request, 2026-09-04).
    /// </summary>
    FieldOfficer = 6,

    /// <summary>
    /// Settles a custom-work obligation (client request, 2026-09-04 — product validation
    /// found custom work had no way to be bank-matched at all). Both
    /// <see cref="ReconciliationAllocationInput.ProjectId"/> and
    /// <see cref="ReconciliationAllocationInput.VendorId"/> (the party) are required —
    /// unlike <see cref="Vendor"/>, there is no project-less "advance" case here.
    /// </summary>
    CustomWork = 7,

    /// <summary>
    /// A project-attributable charge with no party — typically a bank transfer/handling
    /// fee that rode along with a vendor payment (client request, 2026-09-04). Posts as
    /// a Direct Expense under "Other Expenses" for that project.
    /// <see cref="ReconciliationAllocationInput.ProjectId"/> is required;
    /// <see cref="ReconciliationAllocationInput.VendorId"/> must be empty.
    /// </summary>
    BankCharges = 8,
}

/// <summary>
/// One line of a manual debit split. A single bank debit can be split across several
/// of these — vendor(s) (with or without a project) and/or Personal/Office/Savings —
/// as long as their amounts sum to exactly the bank debit (rule 48).
/// </summary>
public sealed record ReconciliationAllocationInput(
    ReconciliationAllocationTarget Target,
    decimal Amount,
    string Description,
    long? ProjectId = null,
    long? VendorId = null);

public sealed record MapDebitRequest(IReadOnlyList<ReconciliationAllocationInput> Allocations);

public sealed record MapDebitResultDto(
    long BankTransactionId,
    string Status,
    IReadOnlyList<long> SettlementIds,
    IReadOnlyList<long> CommonExpenseIds,
    IReadOnlyList<long>? DirectExpenseIds = null);

// ── internal transfers (P4-T08) ──────────────────────────────────────────────

public sealed record PairInternalTransferRequest(long FromTransactionId, long ToTransactionId);

public sealed record InternalTransferDto(
    long Id,
    long FromTransactionId,
    long ToTransactionId,
    long FromAccountId,
    long ToAccountId,
    decimal Amount,
    DateOnly Date);

public sealed record InternalTransferSuggestionDto(
    long DebitTransactionId,
    long CreditTransactionId,
    long FromAccountId,
    long ToAccountId,
    decimal Amount,
    DateOnly Date);

public interface IReconciliationService
{
    Task<ReconciliationQueueDto> QueueAsync(ReconciliationQueueQuery query, CancellationToken cancellationToken);

    Task<ReconciliationProposalDto> ProposalAsync(
        long bankTransactionId, long? vendorId, CancellationToken cancellationToken);

    Task<ReconcileResultDto> ReconcileCreditAsync(
        long bankTransactionId, ReconcileCreditRequest request, CancellationToken cancellationToken);

    Task<ReconcileResultDto> ReconcileDebitAsync(
        long bankTransactionId, ReconcileDebitRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Splits a debit across one or more vendors (with or without a project) and/or
    /// Personal/Office/Savings common expenses in one go (client request, 2026-09-04).
    /// The row must be Pending or on Hold; on success it becomes Reconciled.
    /// </summary>
    Task<MapDebitResultDto> MapDebitAsync(
        long bankTransactionId, MapDebitRequest request, CancellationToken cancellationToken);

    /// <summary>Flags a Pending row "deal with later" (client request, 2026-09-04) — no ledger impact.</summary>
    Task HoldAsync(long bankTransactionId, CancellationToken cancellationToken);

    /// <summary>Reverts a held row back to Pending.</summary>
    Task UnholdAsync(long bankTransactionId, CancellationToken cancellationToken);

    Task UnreconcileAsync(long bankTransactionId, string reason, CancellationToken cancellationToken);

    Task BulkExcludeAsync(IReadOnlyList<long> ids, string reason, CancellationToken cancellationToken);

    Task<IReadOnlyList<InternalTransferSuggestionDto>> TransferSuggestionsAsync(
        long? accountId, CancellationToken cancellationToken);

    Task<InternalTransferDto> PairTransferAsync(
        PairInternalTransferRequest request, CancellationToken cancellationToken);

    Task UnpairTransferAsync(long internalTransferId, CancellationToken cancellationToken);
}

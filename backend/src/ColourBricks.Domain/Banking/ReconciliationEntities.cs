using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Banking;

/// <summary>How a bank transaction was matched to a settlement. TINYINT.</summary>
public enum MatchMethod : byte
{
    /// <summary>Chosen by the accountant with no system suggestion.</summary>
    Manual = 1,

    /// <summary>The accountant confirmed a system-suggested candidate.</summary>
    Suggested = 2,

    /// <summary>Pre-selected above the auto-match threshold, still confirmed by the accountant (BRD §32).</summary>
    Auto = 3,
}

/// <summary>
/// What a <see cref="ReconciliationLink"/> points at (client request, 2026-09-04 — the
/// bank-transaction Map popup can split one debit across a vendor and/or Personal/Office/
/// Savings common expenses). Existing rows are all <see cref="Settlement"/>.
/// </summary>
public enum ReconciliationLinkKind : byte
{
    /// <summary>A vendor/field-officer payment (Out) or a client receipt (In) — <see cref="ReconciliationLink.SettlementId"/>.</summary>
    Settlement = 1,

    /// <summary>A Personal/Office/Savings/Custom map — <see cref="ReconciliationLink.CommonExpenseId"/>.</summary>
    CommonExpense = 2,

    /// <summary>
    /// A custom-work payment (client request, 2026-09-04) — <see cref="ReconciliationLink.SettlementId"/>,
    /// kept distinct from <see cref="Settlement"/> because it reverses through a different
    /// service (<c>ICustomWorkPaymentService</c>, not <c>IVendorPaymentService</c>).
    /// </summary>
    CustomWorkPayment = 3,

    /// <summary>
    /// A project-attributable direct expense — e.g. a bank transfer fee mapped to the
    /// project the payment was for (client request, 2026-09-04) — <see cref="ReconciliationLink.ObligationId"/>.
    /// </summary>
    DirectExpense = 4,
}

/// <summary>
/// Links one <c>BankTransaction</c> to one settlement or common expense once the
/// accountant reconciles it (plan.md §5.2). One bank transaction can now carry
/// several active links (client request, 2026-09-04 — splitting a debit across
/// multiple vendors/projects/common-expense buckets); <see cref="Kind"/> says
/// which target column applies, and <see cref="SettlementCreatedByReconciliation"/>
/// says whether unreconcile must reverse it.
/// </summary>
public sealed class ReconciliationLink : BaseEntity
{
    public long BankTransactionId { get; set; }

    public ReconciliationLinkKind Kind { get; set; } = ReconciliationLinkKind.Settlement;

    public long? SettlementId { get; set; }

    /// <summary>Set only when <see cref="Kind"/> is <see cref="ReconciliationLinkKind.CommonExpense"/>.</summary>
    public long? CommonExpenseId { get; set; }

    /// <summary>Set only when <see cref="Kind"/> is <see cref="ReconciliationLinkKind.DirectExpense"/> (the Obligation row).</summary>
    public long? ObligationId { get; set; }

    public int MatchConfidence { get; set; }

    public MatchMethod MatchMethod { get; set; } = MatchMethod.Manual;

    public bool SettlementCreatedByReconciliation { get; set; }

    public DateTimeOffset? UnlinkedAtUtc { get; set; }

    public long? UnlinkedByUserId { get; set; }

    public string? UnlinkReason { get; set; }
}

/// <summary>
/// A learned mapping from a scrappy bank-narration token to a party (BRD §32). The
/// first time an accountant matches "NEFT/ABCHRDW/12" to ABC Hardware, "ABCHRDW"
/// is remembered so the next such row scores higher.
/// </summary>
public sealed class PartyAlias : BaseEntity
{
    public long PartyId { get; set; }

    public string Alias { get; set; } = "";

    /// <summary>Uppercase, non-alphanumeric stripped. Unique per party.</summary>
    public string NormalisedAlias { get; set; } = "";
}

/// <summary>
/// A pair of bank transactions (one debit, one credit, different accounts) that
/// together are money moving between the company's own accounts (BRD §36). It never
/// touches a project, party, income or expense — only the two account balances.
/// </summary>
public sealed class InternalTransfer : BaseEntity
{
    public long FromTransactionId { get; set; }

    public long ToTransactionId { get; set; }

    public long FromAccountId { get; set; }

    public long ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Date { get; set; }

    public DateTimeOffset? UnpairedAtUtc { get; set; }

    public long? UnpairedByUserId { get; set; }
}

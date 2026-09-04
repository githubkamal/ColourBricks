using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Banking;

/// <summary>
/// A committed bank-statement line (plan.md §5.2). Created only when an
/// <see cref="ImportBatch"/> is committed, always <see cref="BankTransactionStatus.Pending"/>.
/// Never deleted — moved between statuses and excluded with a reason (plan.md §5.6).
/// The <see cref="ProjectHints"/> carry the import-review mapping forward to
/// reconciliation; committing creates no settlement or ledger entry.
/// </summary>
public sealed class BankTransaction : BaseEntity
{
    public long ImportBatchId { get; set; }

    public long AccountId { get; set; }

    public DateOnly ValueDate { get; set; }

    public string Narration { get; set; } = "";

    public string NormalisedNarration { get; set; } = "";

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal? RunningBalance { get; set; }

    public string? BankReference { get; set; }

    /// <summary>SHA-256 of the row's identity fields plus <see cref="OccurrenceIndex"/> — unique.</summary>
    public string RowHash { get; set; } = "";

    /// <summary>Disambiguates genuinely identical same-day rows: the nth such row gets index n.</summary>
    public int OccurrenceIndex { get; set; }

    public BankTransactionStatus Status { get; set; } = BankTransactionStatus.Pending;

    public string? ExclusionReason { get; set; }

    public long? ReconciledByUserId { get; set; }

    public DateTimeOffset? ReconciledAtUtc { get; set; }

    public ICollection<BankTransactionProjectHint> ProjectHints { get; set; } = [];
}

/// <summary>The import-review project mapping, carried as a hint only (P4-T01).
/// Pre-fills the reconciliation screen; it is not a settlement or an allocation.</summary>
public sealed class BankTransactionProjectHint : BaseEntity
{
    public long BankTransactionId { get; set; }

    public long ProjectId { get; set; }

    public decimal Amount { get; set; }
}

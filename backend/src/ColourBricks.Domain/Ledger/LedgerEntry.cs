using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Ledger;

/// <summary>
/// One append-only line in the single financial ledger (plan.md §5.2). Never updated,
/// never deleted — a reversal writes a new mirrored row (plan.md §5.6). The
/// SaveChanges guard enforces this.
/// </summary>
/// <remarks>
/// Minimal shape for P0-T06 (guard + immutability). P2-T01 adds
/// <c>ILedgerPostingService</c>, expense categories and the balance queries, and makes
/// <c>DbSet&lt;LedgerEntry&gt;</c> inaccessible outside Infrastructure.
/// </remarks>
public sealed class LedgerEntry : BaseEntity
{
    public DateOnly EntryDate { get; init; }

    public long? ProjectId { get; init; }

    public long? AccountId { get; init; }

    public long? PartyId { get; init; }

    public long CategoryId { get; init; }

    public decimal Debit { get; init; }

    public decimal Credit { get; init; }

    /// <summary>The kind of record that produced this entry (e.g. "VendorPayment").</summary>
    public required string SourceType { get; init; }

    public long SourceId { get; init; }

    public bool IsReversal { get; init; }
}

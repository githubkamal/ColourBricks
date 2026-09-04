namespace ColourBricks.Application.Ledger;

/// <summary>
/// One leg of a ledger posting. A leg is a debit <em>or</em> a credit against a
/// category, optionally scoped to a project, account and party (plan.md §5.2).
/// </summary>
public sealed record LedgerLeg(
    long CategoryId,
    decimal Debit,
    decimal Credit,
    long? ProjectId = null,
    long? AccountId = null,
    long? PartyId = null);

/// <summary>
/// A balanced set of ledger legs written together, tagged with the record that
/// produced them so every entry traces back (plan.md §5.2, P2-T01 acceptance).
/// </summary>
public sealed record LedgerPosting(
    string SourceType,
    long SourceId,
    DateOnly EntryDate,
    IReadOnlyList<LedgerLeg> Legs);

public sealed record ExpenseCategoryDto(
    long Id, string Name, string Slug, string Bucket, bool IsCost, bool IsActive);

public sealed record ProjectLedgerRowDto(
    long Id,
    DateOnly EntryDate,
    long CategoryId,
    string CategoryName,
    string Bucket,
    long? AccountId,
    long? PartyId,
    decimal Debit,
    decimal Credit,
    string SourceType,
    long SourceId,
    bool IsReversal);

/// <summary>The source has already been reversed.</summary>
public sealed class LedgerAlreadyReversedException(string sourceType, long sourceId)
    : Exception($"{sourceType} #{sourceId} has already been reversed.");

/// <summary>There is nothing posted for the given source.</summary>
public sealed class LedgerSourceNotFoundException(string sourceType, long sourceId)
    : Exception($"Nothing is posted for {sourceType} #{sourceId}.");

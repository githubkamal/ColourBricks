namespace ColourBricks.Application.Banking;

/// <summary>One extracted statement line handed to the staging service (P4-T02 parses these; tests post them directly).</summary>
public sealed record ParsedBankRowInput(
    int SourceLineNo,
    DateOnly? ValueDate,
    string? Narration,
    decimal Debit,
    decimal Credit,
    decimal? Balance = null,
    string? BankReference = null,
    string? ParseError = null,
    string? RawLine = null);

public sealed record CreateBankImportRequest(
    long AccountId,
    string FileName,
    IReadOnlyList<ParsedBankRowInput> Rows);

/// <summary>One row from a dry-run parse (<see cref="IBankImportService.PreviewAsync"/>) — nothing
/// behind this has been persisted. <see cref="ExistsInDb"/> is only populated when the caller asked
/// for the DB check; otherwise it's always false.</summary>
public sealed record PreviewBankImportRowDto(
    int SourceLineNo,
    DateOnly? ValueDate,
    string? Narration,
    decimal Debit,
    decimal Credit,
    decimal? Balance,
    string? BankReference,
    string? ParseError,
    bool ExistsInDb);

public sealed record ProjectAllocationInput(long ProjectId, decimal Amount);

public sealed record SetRowAllocationsRequest(IReadOnlyList<ProjectAllocationInput> Allocations);

public sealed record ProjectAllocationDto(long ProjectId, string ProjectName, decimal Amount);

public sealed record StagedBankRowDto(
    long Id,
    int SourceLineNo,
    DateOnly? ValueDate,
    string Narration,
    decimal Debit,
    decimal Credit,
    decimal? Balance,
    string? BankReference,
    string ParseState,
    string? ParseError,
    long? DuplicateOfBankTransactionId,
    bool IsRemoved,
    IReadOnlyList<ProjectAllocationDto> Allocations,
    decimal AllocatedTotal,
    bool ReadyToCommit,
    string? BlockedReason);

public sealed record BankImportOutcomeCountsDto(
    int Total,
    int Mapped,
    int Unmapped,
    int Removed,
    int Duplicate,
    int ParseError,
    int Committed);

public sealed record BankImportBatchDto(
    long Id,
    long AccountId,
    string FileName,
    string Status,
    DateTimeOffset UploadedAtUtc,
    long? UploadedByUserId,
    BankImportOutcomeCountsDto Counts,
    IReadOnlyList<StagedBankRowDto> Rows);

public sealed record BankImportCommitResultDto(
    long BatchId,
    int Committed,
    int Removed,
    int Duplicate,
    int ParseError);

public sealed record BankTransactionDto(
    long Id,
    long ImportBatchId,
    long AccountId,
    DateOnly ValueDate,
    string Narration,
    decimal Debit,
    decimal Credit,
    decimal? RunningBalance,
    string? BankReference,
    int OccurrenceIndex,
    string Status,
    string? ExclusionReason,
    IReadOnlyList<ProjectAllocationDto> ProjectHints);

public sealed record ExcludeBankTransactionRequest(string Reason);

public interface IBankImportService
{
    Task<BankImportBatchDto> CreateDraftAsync(CreateBankImportRequest request, CancellationToken cancellationToken);

    /// <summary>Parse an uploaded statement against a saved profile and stage it as a Draft batch (P4-T02).</summary>
    Task<BankImportBatchDto> CreateDraftFromFileAsync(
        long accountId, string fileName, Stream content, long profileId, CancellationToken cancellationToken);

    /// <summary>Dry-run parse: reads the file against an ad-hoc (never persisted) column mapping and,
    /// optionally, flags rows whose date+amount+narration+reference signature already exists in
    /// <c>BankTransactions</c> for the account. Nothing is written to the database.</summary>
    Task<IReadOnlyList<PreviewBankImportRowDto>> PreviewAsync(
        long accountId, BankStatementProfileDto adHocProfile, Stream content, string fileName,
        bool checkExisting, CancellationToken cancellationToken);

    Task<BankImportBatchDto?> GetAsync(long batchId, CancellationToken cancellationToken);

    Task<StagedBankRowDto> SetRowAllocationsAsync(
        long batchId, long rowId, SetRowAllocationsRequest request, CancellationToken cancellationToken);

    /// <summary>Discards a staged (pre-commit) row and logs <paramref name="reason"/> to the audit trail (BRD §65).</summary>
    Task<bool> RemoveRowAsync(long batchId, long rowId, string reason, CancellationToken cancellationToken);

    Task<BankImportCommitResultDto> CommitAsync(long batchId, CancellationToken cancellationToken);

    Task<bool> DiscardAsync(long batchId, CancellationToken cancellationToken);

    Task<BankTransactionDto?> GetTransactionAsync(long id, CancellationToken cancellationToken);

    Task<bool> ExcludeTransactionAsync(long id, string reason, CancellationToken cancellationToken);
}

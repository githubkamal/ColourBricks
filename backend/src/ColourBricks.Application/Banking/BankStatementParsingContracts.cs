namespace ColourBricks.Application.Banking;

public sealed record BankStatementProfileDto(
    long Id,
    long AccountId,
    string Name,
    int HeaderRowIndex,
    string Delimiter,
    int DateColumn,
    int NarrationColumn,
    int? ReferenceColumn,
    int? BalanceColumn,
    bool SingleAmountColumn,
    int? AmountColumn,
    int? DebitColumn,
    int? CreditColumn,
    string DebitSign,
    string DateFormats);

public sealed record CreateBankStatementProfileRequest(
    long AccountId,
    string Name,
    int HeaderRowIndex,
    int DateColumn,
    int NarrationColumn,
    bool SingleAmountColumn,
    string DateFormats,
    string Delimiter = ",",
    int? ReferenceColumn = null,
    int? BalanceColumn = null,
    int? AmountColumn = null,
    int? DebitColumn = null,
    int? CreditColumn = null,
    string DebitSign = "Negative");

/// <summary>The header cells and a few sample data rows, for the mapping wizard.</summary>
public sealed record DetectedColumnsDto(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> SampleRows);

/// <summary>The outcome of parsing an uploaded statement against a profile.</summary>
public sealed record BankStatementParseResult(
    IReadOnlyList<ParsedBankRowInput> Rows,
    int TotalDataRows,
    int ErrorRows);

public interface IBankStatementParser
{
    /// <summary>Reads the file's header row (at <paramref name="headerRowIndex"/>) plus up to five sample rows.</summary>
    DetectedColumnsDto DetectColumns(Stream content, string fileName, int headerRowIndex);

    /// <summary>Streams the file and maps each data row to a <see cref="ParsedBankRowInput"/>. A bad row
    /// yields an error row (never throws); a file that cannot be read at all does throw.</summary>
    BankStatementParseResult Parse(Stream content, string fileName, BankStatementProfileDto profile);
}

public interface IBankStatementProfileService
{
    Task<BankStatementProfileDto> CreateAsync(
        CreateBankStatementProfileRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<BankStatementProfileDto>> ListForAccountAsync(
        long accountId, CancellationToken cancellationToken);

    Task<BankStatementProfileDto?> GetAsync(long id, CancellationToken cancellationToken);
}

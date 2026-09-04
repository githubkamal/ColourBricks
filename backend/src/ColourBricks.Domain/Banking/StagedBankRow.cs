using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Banking;

/// <summary>
/// A single extracted statement line awaiting review (P4-T01). The accountant maps
/// it to project(s) via <see cref="Allocations"/> and either keeps it (it commits to
/// a <c>BankTransaction</c>) or removes it (<see cref="IsRemoved"/> — dropped, with
/// no fingerprint kept, so a re-import of the same file surfaces it again).
/// </summary>
public sealed class StagedBankRow : BaseEntity
{
    public long ImportBatchId { get; set; }

    public int SourceLineNo { get; set; }

    /// <summary>Null only when <see cref="ParseState"/> is <see cref="StagedRowState.Error"/>.</summary>
    public DateOnly? ValueDate { get; set; }

    public string Narration { get; set; } = "";

    /// <summary>Uppercased, whitespace-collapsed narration — feeds the RowHash so it is whitespace-stable.</summary>
    public string NormalisedNarration { get; set; } = "";

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal? Balance { get; set; }

    public string? BankReference { get; set; }

    public StagedRowState ParseState { get; set; } = StagedRowState.Parsed;

    public string? ParseError { get; set; }

    public string? RawLine { get; set; }

    /// <summary>Set when this row's prospective RowHash already exists as a committed transaction.</summary>
    public long? DuplicateOfBankTransactionId { get; set; }

    public bool IsRemoved { get; set; }

    public ICollection<StagedBankRowAllocation> Allocations { get; set; } = [];
}

/// <summary>One project slice of a staged row (P4-T01). Debit rows may have many
/// (summing to the debit); credit rows have exactly one (BRD §34 / rule 46).</summary>
public sealed class StagedBankRowAllocation : BaseEntity
{
    public long StagedBankRowId { get; set; }

    public long ProjectId { get; set; }

    public decimal Amount { get; set; }
}

using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Banking;

/// <summary>
/// One upload of a bank statement (BRD §30–§31, plan.md §5.2). An upload lands as a
/// <see cref="ImportBatchStatus.Draft"/> batch of <see cref="StagedBankRow"/> for
/// review; only <c>Commit</c> promotes the surviving, project-mapped rows to
/// <c>BankTransaction</c>. Row counts by outcome are derived from the staged rows,
/// never stored (plan.md §5.3).
/// </summary>
public sealed class ImportBatch : BaseEntity
{
    public long AccountId { get; set; }

    public string FileName { get; set; } = "";

    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Draft;

    public ICollection<StagedBankRow> Rows { get; set; } = [];
}

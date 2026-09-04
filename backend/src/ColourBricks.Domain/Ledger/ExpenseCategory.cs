using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Ledger;

/// <summary>
/// A cost/income category every <see cref="LedgerEntry"/> is tagged with (BRD §7).
/// <see cref="Bucket"/> is the label the project dashboard's expense breakdown groups
/// by (BRD §5). Seeded and system-owned; an Administrator may add more.
/// </summary>
[Auditable("project_expenses")]
public sealed class ExpenseCategory : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Stable machine key the posting services reference. Unique.</summary>
    public required string Slug { get; set; }

    /// <summary>The BRD §5 expense-breakdown bucket, or <c>Income</c> / <c>Liability</c>.</summary>
    public required string Bucket { get; set; }

    /// <summary>True for a cost bucket that rolls into "Total Expenses" (BRD §5).</summary>
    public bool IsCost { get; set; } = true;

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;
}

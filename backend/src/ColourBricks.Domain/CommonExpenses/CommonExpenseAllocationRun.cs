using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.CommonExpenses;

/// <summary>Equal | Percentage | Manual (BRD §46). Stored as text for readability.</summary>
public static class CommonExpenseAllocationMethod
{
    public const string Equal = "Equal";
    public const string Percentage = "Percentage";
    public const string Manual = "Manual";

    public static bool IsValid(string? m) => m is Equal or Percentage or Manual;
}

/// <summary>
/// One distribution of a period's unallocated Personal / Office / Savings expenses
/// across the ongoing projects (BRD §45, §47). Immutable once committed; a whole run
/// is reversed, never edited (P6-T04).
/// </summary>
public sealed class CommonExpenseAllocationRun : BaseEntity
{
    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    /// <summary>Comma-separated <see cref="CommonExpenseType"/> names that were pooled.</summary>
    public string Types { get; set; } = "";

    public string Method { get; set; } = CommonExpenseAllocationMethod.Equal;

    public decimal PoolAmount { get; set; }

    public CommonExpenseStatus Status { get; set; } = CommonExpenseStatus.Active;

    public string? Note { get; set; }

    public ICollection<CommonExpenseAllocationLine> Lines { get; set; } = [];
}

public sealed class CommonExpenseAllocationLine : BaseEntity
{
    public long RunId { get; set; }

    public long ProjectId { get; set; }

    public decimal Amount { get; set; }

    public decimal? Percent { get; set; }
}

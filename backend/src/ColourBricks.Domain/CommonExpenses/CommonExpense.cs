using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.CommonExpenses;

/// <summary>Personal, Office, Savings or Custom — company-level, not tied to a project (BRD §44). TINYINT.</summary>
public enum CommonExpenseType : byte
{
    Personal = 1,
    Office = 2,
    Savings = 3,

    /// <summary>A catch-all bucket for spend that isn't Personal/Office/Savings (client request, 2026-09-04).</summary>
    Custom = 4,
}

/// <summary>Cancelled by reversal, never deleted (plan.md §5.6). TINYINT.</summary>
public enum CommonExpenseStatus : byte
{
    Active = 1,
    Reversed = 2,
}

/// <summary>
/// A company-level Personal / Office / Savings expense (BRD §44). It posts to the
/// ledger with no project; a later allocation run (P6-T02/T03) distributes it to
/// ongoing projects. Savings is tracked but is not a P&amp;L expense.
/// </summary>
public sealed class CommonExpense : BaseEntity
{
    public CommonExpenseType Type { get; set; }

    public string SubCategory { get; set; } = "";

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    public long PaymentModeId { get; set; }

    public long? AccountId { get; set; }

    public string? ReferenceNo { get; set; }

    public string? Description { get; set; }

    public CommonExpenseStatus Status { get; set; } = CommonExpenseStatus.Active;

    /// <summary>The allocation run that distributed this expense to projects (P6-T02/T03). Null = not yet allocated.</summary>
    public long? AllocationRunId { get; set; }
}


using ColourBricks.Domain.Common;
using ColourBricks.Domain.CommonExpenses;

namespace ColourBricks.Domain.FieldOfficers;

/// <summary>Cancelled by reversal, never deleted (plan.md §5.6). TINYINT.</summary>
public enum FieldOfficerExpenseStatus : byte
{
    Active = 1,
    Reversed = 2,
}

/// <summary>
/// A field officer's bill that is NOT tied to a project — Personal/Office/Savings/
/// Custom (client request, 2026-09-04). Unlike <see cref="CommonExpense"/> (which
/// pays the company's own cash instantly), this is money the officer already spent
/// on the company's behalf: it posts a payable to him (the same "vendor_payable"
/// ledger category and party-generic outstanding/advance derivation a real vendor
/// purchase uses), not an immediate cash outflow. It carries no <c>ProjectId</c>
/// because <see cref="Obligations.Obligation"/> requires one and this deliberately
/// doesn't — see <see cref="CommonExpenseType"/> for the four buckets it can target.
/// </summary>
public sealed class FieldOfficerExpense : BaseEntity
{
    public long FieldOfficerId { get; set; }

    public CommonExpenseType Type { get; set; }

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    public string? ReferenceNo { get; set; }

    public string? Description { get; set; }

    public FieldOfficerExpenseStatus Status { get; set; } = FieldOfficerExpenseStatus.Active;
}

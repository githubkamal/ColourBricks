using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Payments;

/// <summary>
/// A configurable way money moves — Cash, Bank Transfer, UPI, Cheque, … (BRD §28,
/// §70 rule 18). Every applicable financial transaction records a payment mode
/// (BRD §29). Deactivated rather than deleted so historical transactions keep
/// rendering the mode's name (plan.md §5.6).
/// </summary>
[Auditable("accounts")]
public sealed class PaymentMode : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Lowercase, punctuation-stripped, whitespace-collapsed. Unique (plan.md §6).</summary>
    public required string NormalisedName { get; set; }

    /// <summary>The entry must name a cash/bank account (true for all but cash-in-hand).</summary>
    public bool RequiresAccount { get; set; } = true;

    /// <summary>The entry must carry a reference number (Cheque, UPI, Bank Transfer).</summary>
    public bool RequiresReference { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Loans;

/// <summary>An EMI payment is cancelled by reversal, never deleted. TINYINT.</summary>
public enum LoanEmiPaymentStatus : byte
{
    Active = 1,
    Reversed = 2,
}

/// <summary>
/// A payment against a loan (BRD §48). Every payment carries its own
/// principal / interest split: interest is a cost, principal only reduces the
/// liability. A prepayment is all principal and has no instalment.
/// </summary>
[Auditable("emi")]
public sealed class LoanEmiPayment : BaseEntity
{
    public long LoanId { get; set; }

    /// <summary>The scheduled instalment settled, or null for a prepayment lump.</summary>
    public long? InstalmentId { get; set; }

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    public decimal PrincipalPaid { get; set; }

    public decimal InterestPaid { get; set; }

    public long PaymentModeId { get; set; }

    public long? AccountId { get; set; }

    public string? ReferenceNo { get; set; }

    public bool IsPrepayment { get; set; }

    public LoanEmiPaymentStatus Status { get; set; } = LoanEmiPaymentStatus.Active;
}

using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Loans;

/// <summary>A loan is closed by reversal or full repayment, never deleted. TINYINT.</summary>
public enum LoanStatus : byte
{
    Active = 1,
    Closed = 2,
    Reversed = 3,
}

/// <summary>
/// A borrowing (BRD §48). Disbursement lands in a bank account and raises a
/// liability at the lender; it is never project income (plan.md §5.3). Outstanding
/// principal is always derived from the amortisation schedule and payments, never
/// stored (BRD §48 acceptance).
/// </summary>
[Auditable("loans")]
public sealed class Loan : BaseEntity
{
    /// <summary>The project the loan funds, or null for a company-level loan.</summary>
    public long? ProjectId { get; set; }

    /// <summary>The lender — a <c>Party</c> carrying <c>PartyType.Lender</c>.</summary>
    public long LenderId { get; set; }

    /// <summary>The amount borrowed and disbursed.</summary>
    public decimal PrincipalAmount { get; set; }

    /// <summary>Annual reducing-balance interest rate, as a percentage (e.g. 9.00).</summary>
    public decimal AnnualInterestRatePercent { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Number of monthly instalments.</summary>
    public int TenureMonths { get; set; }

    /// <summary>
    /// Client-supplied EMI when it differs from the computed reducing-balance figure
    /// (BRD §48). Null until set; the schedule (P7-T02) fills or overrides it.
    /// </summary>
    public decimal? EmiAmount { get; set; }

    public DateOnly EmiStartDate { get; set; }

    /// <summary>Last instalment date. Derived when the schedule is generated (P7-T02).</summary>
    public DateOnly? EmiEndDate { get; set; }

    /// <summary>The bank account the disbursement is credited to.</summary>
    public long DisbursementAccountId { get; set; }

    public DateOnly DisbursementDate { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public LoanStatus Status { get; set; } = LoanStatus.Active;
}

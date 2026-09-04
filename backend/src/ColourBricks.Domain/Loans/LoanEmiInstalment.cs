using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Loans;

/// <summary>Where a scheduled instalment stands. TINYINT.</summary>
public enum LoanEmiStatus : byte
{
    Pending = 1,
    PartPaid = 2,
    Paid = 3,
}

/// <summary>
/// One row of a loan's amortisation schedule (BRD §48, §54). Generated from
/// <see cref="Services.EmiAmortisation"/>; regenerated on a rate change without
/// touching rows already <see cref="LoanEmiStatus.Paid"/>.
/// </summary>
[Auditable("emi")]
public sealed class LoanEmiInstalment : BaseEntity
{
    public long LoanId { get; set; }

    /// <summary>1-based position in the schedule.</summary>
    public int InstalmentNo { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal OpeningPrincipal { get; set; }

    public decimal EmiAmount { get; set; }

    public decimal PrincipalComponent { get; set; }

    public decimal InterestComponent { get; set; }

    public decimal ClosingPrincipal { get; set; }

    public LoanEmiStatus Status { get; set; } = LoanEmiStatus.Pending;

    /// <summary>Total settled against this instalment so far (part payments accumulate).</summary>
    public decimal PaidAmount { get; set; }

    public DateOnly? PaidDate { get; set; }
}

using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Loans;

/// <summary>Why a loan alert was raised. TINYINT.</summary>
public enum LoanAlertKind : byte
{
    UpcomingEmi = 1,
    OverdueEmi = 2,
}

/// <summary>
/// A raised loan alert (BRD §48, §66). One row per (instalment, kind) so a run
/// never re-raises an alert that already exists; it is resolved once the
/// instalment is paid. Feeds the notification system in P9-T01.
/// </summary>
[Auditable("loans")]
public sealed class LoanAlert : BaseEntity
{
    public long LoanId { get; set; }

    public long InstalmentId { get; set; }

    public LoanAlertKind Kind { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal Amount { get; set; }

    public DateOnly RaisedOn { get; set; }

    public DateOnly? ResolvedOn { get; set; }
}

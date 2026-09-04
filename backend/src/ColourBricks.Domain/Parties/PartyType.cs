namespace ColourBricks.Domain.Parties;

/// <summary>
/// The roles a counterparty can play (plan.md §5.2). A flag set — a hardware shop can
/// also be a subcontractor, so one <c>Party</c> row can hold several.
/// </summary>
[Flags]
public enum PartyType
{
    None = 0,
    Vendor = 1,
    Subcontractor = 2,
    Client = 4,
    Temple = 8,
    Lender = 16,

    /// <summary>
    /// An internal cash-handling agent who buys on the company's behalf — for a
    /// project or for Personal/Office/Savings/Custom — and carries a running,
    /// bidirectional balance (client request, 2026-09-04). Reuses the exact vendor
    /// payable/advance ledger machinery ("vendor_payable" category, party-generic
    /// outstanding/advance derivation) rather than a parallel one.
    /// </summary>
    FieldOfficer = 32,
}

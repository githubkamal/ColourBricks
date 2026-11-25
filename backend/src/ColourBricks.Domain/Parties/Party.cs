using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Parties;

/// <summary>
/// A counterparty — vendor, subcontractor, client, temple or lender (plan.md §5.2,
/// BRD §9, §12, §26). One table for all of them; <see cref="Types"/> records which
/// roles this party plays. Created once and reused (BRD §11, §13).
/// </summary>
[Auditable("vendors")]
public sealed class Party : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Lowercase, punctuation-stripped, whitespace-collapsed. Unique (plan.md §6).</summary>
    public required string NormalisedName { get; set; }

    public PartyType Types { get; set; }

    public string? Category { get; set; }

    public string? ContactPerson { get; set; }

    public string? Phone { get; set; }

    /// <summary>Second contact number — team master (client request, 2026-11-21).</summary>
    public string? AlternatePhone { get; set; }

    /// <summary>Area/locality a subcontractor team works in (client request, 2026-11-21).</summary>
    public string? WorkingArea { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? GstNumber { get; set; }

    /// <summary>Free-text bank details (legacy). New entries use the structured fields below.</summary>
    public string? BankDetails { get; set; }

    public string? BankAccountHolderName { get; set; }

    public string? BankName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BankIfsc { get; set; }

    public string? PaymentTerms { get; set; }

    /// <summary>Department for subcontractor teams (BRD §9). FK added in P1-T04.</summary>
    public long? DepartmentId { get; set; }

    public bool IsActive { get; set; } = true;
}

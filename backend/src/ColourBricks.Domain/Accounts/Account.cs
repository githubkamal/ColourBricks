using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Accounts;

/// <summary>
/// A cash box or bank account money moves through (BRD §29, §70 rule 19). The
/// running balance is always derived from <c>LedgerEntry</c> — never stored
/// (plan.md §5.3). Deactivated rather than deleted so historical transactions keep
/// rendering the account name (plan.md §5.6).
/// </summary>
[Auditable("accounts")]
public sealed class Account : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Lowercase, punctuation-stripped, whitespace-collapsed. Unique (plan.md §6).</summary>
    public required string NormalisedName { get; set; }

    public AccountType Type { get; set; }

    public string? BankName { get; set; }

    /// <summary>Stored in full; masked to the last four digits for non-Admin users (BRD §29).</summary>
    public string? AccountNumber { get; set; }

    public string? Ifsc { get; set; }

    /// <summary>
    /// The balance before any recorded transaction. Immutable once a ledger entry
    /// exists for this account (P1-T06 acceptance).
    /// </summary>
    public decimal OpeningBalance { get; set; }

    public DateOnly OpeningBalanceDate { get; set; }

    public bool IsActive { get; set; } = true;
}

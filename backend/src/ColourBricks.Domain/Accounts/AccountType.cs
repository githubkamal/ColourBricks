namespace ColourBricks.Domain.Accounts;

/// <summary>Cash-in-hand or a bank account (BRD §29). Stored as TINYINT (plan.md §6).</summary>
public enum AccountType : byte
{
    Cash = 1,
    Bank = 2,
}

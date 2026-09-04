namespace ColourBricks.Domain.Obligations;

/// <summary>What kind of thing is owed (plan.md §5.1). TINYINT.</summary>
public enum ObligationType : byte
{
    VendorPurchase = 1,
    SubcontractorWork = 2,
    CustomWork = 3,
    TempleDonation = 4,
    DirectExpense = 5,
}

/// <summary>An obligation is cancelled by reversal, never deleted (plan.md §5.6). TINYINT.</summary>
public enum ObligationStatus : byte
{
    Active = 1,
    Reversed = 2,
}

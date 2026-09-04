namespace ColourBricks.Domain.Settlements;

/// <summary>How often a labour/subcontractor payment recurs (BRD §10). TINYINT.</summary>
public enum PaymentFrequency : byte
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Milestone = 4,
    AdHoc = 5,
}

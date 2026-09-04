namespace ColourBricks.Domain.Settlements;

/// <summary>Which way money moves in a <see cref="Settlement"/> (plan.md §5.2). TINYINT.</summary>
public enum SettlementDirection : byte
{
    In = 1,
    Out = 2,
}

/// <summary>Kind of project income (BRD §6). TINYINT.</summary>
public enum IncomeType : byte
{
    ClientAdvance = 1,
    Stage = 2,
    Milestone = 3,
    Additional = 4,
    Final = 5,
    Other = 6,
}

/// <summary>A settlement is cancelled by reversal, never deleted (plan.md §5.6). TINYINT.</summary>
public enum SettlementStatus : byte
{
    Active = 1,
    Reversed = 2,
}

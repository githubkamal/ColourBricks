namespace ColourBricks.Domain.Banking;

/// <summary>Lifecycle of a bank-statement import (P4-T01). TINYINT.</summary>
public enum ImportBatchStatus : byte
{
    /// <summary>Rows are staged for review; nothing is in <c>BankTransaction</c> yet.</summary>
    Draft = 1,

    /// <summary>Surviving rows have been promoted to <c>BankTransaction</c>; the batch is read-only.</summary>
    Committed = 2,

    /// <summary>The accountant abandoned the import; the staged rows are kept only for the record.</summary>
    Discarded = 3,
}

/// <summary>Whether a staged row parsed cleanly (P4-T01). TINYINT.</summary>
public enum StagedRowState : byte
{
    Parsed = 1,
    Error = 2,
}

/// <summary>
/// Reconciliation state of a committed bank transaction (BRD §33, plan.md §5.2). TINYINT.
/// A transaction is only ever moved between these — never deleted (plan.md §5.6).
/// </summary>
public enum BankTransactionStatus : byte
{
    Pending = 1,
    InReview = 2,
    Reconciled = 3,
    Excluded = 4,
    InternalTransfer = 5,
}

/// <summary>
/// What one slice of an imported bank row is mapped to at import review (client request,
/// 2026-09-24 — a row can go to a vendor, field officer, labour team, client or a common
/// bucket, not only a project). TINYINT. Party targets carry a <c>PartyId</c> and an
/// optional project; <see cref="Project"/> carries a project only; the buckets carry neither.
/// </summary>
public enum BankRowMappingTarget : byte
{
    Project = 1,
    Vendor = 2,
    FieldOfficer = 3,

    /// <summary>A labour team — a <c>Party</c> with the Subcontractor role.</summary>
    Labour = 4,
    Client = 5,
    Personal = 6,
    Office = 7,
    Savings = 8,
    Other = 9,
}

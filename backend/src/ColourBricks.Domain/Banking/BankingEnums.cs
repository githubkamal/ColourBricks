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

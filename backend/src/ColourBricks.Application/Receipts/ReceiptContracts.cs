using ColourBricks.Domain.Settlements;

namespace ColourBricks.Application.Receipts;

public sealed record ReceiptDto(
    long Id,
    long ProjectId,
    IncomeType Type,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    string PaymentModeName,
    long? AccountId,
    string? AccountName,
    string? ReferenceNo,
    string? Description,
    SettlementStatus Status,
    string ConcurrencyStamp);

/// <summary>
/// Records one project income receipt (BRD §6). Exactly one project — the request
/// carries a single <see cref="ProjectId"/>; there is no way to attach more
/// (BRD §70 rule 46).
/// </summary>
public sealed record RecordReceiptRequest(
    long ProjectId,
    IncomeType Type,
    DateOnly Date,
    decimal Amount,
    long PaymentModeId,
    long? AccountId = null,
    string? ReferenceNo = null,
    string? Description = null);

public sealed record ReverseReceiptRequest(string Reason);

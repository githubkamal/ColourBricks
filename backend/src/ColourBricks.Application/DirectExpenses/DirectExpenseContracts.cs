namespace ColourBricks.Application.DirectExpenses;

public sealed record DirectExpenseDto(
    long Id,
    long ProjectId,
    long CategoryId,
    string CategoryName,
    string Bucket,
    long? PartyId,
    DateOnly Date,
    decimal Amount,
    bool PaidImmediately,
    string? Description,
    string Status);

public sealed record RecordDirectExpenseRequest(
    long ProjectId,
    long CategoryId,
    DateOnly Date,
    decimal Amount,
    bool PaidImmediately = false,
    long? PartyId = null,
    long? PaymentModeId = null,
    long? AccountId = null,
    string? ReferenceNo = null,
    string? Description = null);

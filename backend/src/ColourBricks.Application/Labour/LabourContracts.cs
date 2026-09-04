using ColourBricks.Domain.Settlements;

namespace ColourBricks.Application.Labour;

public sealed record WorkEntryDto(
    long Id,
    long ProjectId,
    long? DepartmentId,
    long TeamId,
    string TeamName,
    DateOnly Date,
    string? WorkType,
    string? Description,
    decimal AgreedValue,
    decimal TotalPaid,
    decimal Outstanding,
    string Status);

public sealed record WorkPaymentDto(
    long Id,
    long WorkEntryId,
    DateOnly Date,
    decimal Amount,
    PaymentFrequency Frequency,
    long PaymentModeId,
    long? AccountId,
    string? ReferenceNo);

public sealed record RecordWorkRequest(
    long ProjectId,
    long TeamId,
    DateOnly Date,
    decimal AgreedValue,
    long? DepartmentId = null,
    string? WorkType = null,
    string? Description = null);

public sealed record PayWorkRequest(
    DateOnly Date,
    decimal Amount,
    PaymentFrequency Frequency,
    long PaymentModeId,
    long? AccountId = null,
    string? ReferenceNo = null);

/// <summary>One line of a team statement (BRD §10) with the running outstanding after it.</summary>
public sealed record TeamStatementRowDto(
    DateOnly Date,
    string Kind,          // "Work" | "Payment"
    string Reference,
    decimal WorkValue,
    decimal Paid,
    decimal RunningOutstanding);

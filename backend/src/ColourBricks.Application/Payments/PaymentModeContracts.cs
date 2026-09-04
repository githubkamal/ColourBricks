namespace ColourBricks.Application.Payments;

public sealed record PaymentModeDto(
    long Id,
    string Name,
    bool RequiresAccount,
    bool RequiresReference,
    int SortOrder,
    bool IsActive,
    string ConcurrencyStamp);

public sealed record CreatePaymentModeRequest(
    string Name,
    bool RequiresAccount = true,
    bool RequiresReference = false,
    int SortOrder = 100);

public sealed record UpdatePaymentModeRequest(
    string Name,
    bool RequiresAccount,
    bool RequiresReference,
    bool IsActive,
    string ConcurrencyStamp,
    int SortOrder = 100);

/// <summary>
/// The payment-mode-specific part of any settlement (BRD §28, §29). Every
/// settlement command from P2 onward embeds one and calls
/// <c>IPaymentModeService.ValidateInstructionAsync</c> before posting.
/// </summary>
public sealed record PaymentInstruction(long PaymentModeId, string? ReferenceNo, long? AccountId);

public sealed class PaymentModeExactDuplicateException(long existingId, string name)
    : Exception($"A payment mode named '{name}' already exists.")
{
    public long ExistingId { get; } = existingId;
}

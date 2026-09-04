namespace ColourBricks.Application.Payments;

public interface IPaymentModeService
{
    /// <summary>Active modes only unless <paramref name="includeInactive"/> is set.</summary>
    Task<IReadOnlyList<PaymentModeDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<PaymentModeDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<PaymentModeDto> CreateAsync(CreatePaymentModeRequest request, CancellationToken cancellationToken);

    Task<PaymentModeDto?> UpdateAsync(
        long id, UpdatePaymentModeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Throws <see cref="FluentValidation.ValidationException"/> (→ 400) when the chosen
    /// mode is inactive, or its <c>RequiresReference</c> / <c>RequiresAccount</c> flags
    /// are not satisfied. Composed by every settlement command from P2 onward.
    /// </summary>
    Task ValidateInstructionAsync(PaymentInstruction instruction, CancellationToken cancellationToken);
}

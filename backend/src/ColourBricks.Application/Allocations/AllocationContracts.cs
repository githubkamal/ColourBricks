namespace ColourBricks.Application.Allocations;

public sealed record AllocationLineDto(
    long ProjectId,
    string ProjectName,
    long? ObligationId,
    string? ObligationReference,
    decimal OutstandingBefore,
    decimal Allocated,
    decimal OutstandingAfter);

public sealed record AllocationProposalDto(
    long PartyId,
    decimal Amount,
    string Method,
    IReadOnlyList<AllocationLineDto> Lines,
    decimal TotalAllocated,
    decimal Advance);

public sealed record AllocationInputDto(long ProjectId, long? ObligationId, decimal Amount);

/// <summary>
/// FIFO / manual multi-project payment allocation (BRD §19–§21, §23–§24). Oldest
/// obligation first; the party's open obligations are row-locked for the duration of
/// <see cref="ApplyAsync"/> so two concurrent payments cannot over-allocate.
/// </summary>
public interface IAllocationEngine
{
    Task<AllocationProposalDto> ProposeAsync(
        long partyId, decimal amount, string method, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the allocation rows for a settlement under a row lock. Throws
    /// <see cref="AllocationExceedsOutstandingException"/> if a slice is bigger than the
    /// obligation's remaining outstanding (checked inside the lock), and
    /// <see cref="AllocationSumMismatchException"/> if the slices total more than the
    /// settlement amount.
    /// </summary>
    Task ApplyAsync(
        long settlementId, IReadOnlyList<AllocationInputDto> allocations, string method,
        CancellationToken cancellationToken);
}

public sealed class AllocationExceedsOutstandingException(long obligationId, decimal requested, decimal available)
    : Exception($"Allocation of {requested:0.00} to obligation #{obligationId} exceeds its remaining {available:0.00}.");

public sealed class AllocationSumMismatchException(decimal allocated, decimal settlementAmount)
    : Exception($"Allocations total {allocated:0.00} but the payment is {settlementAmount:0.00}.");

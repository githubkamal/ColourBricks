namespace ColourBricks.Application.Donations;

public interface IProjectDonationService
{
    Task<ProjectDonationDto?> GetForProjectAsync(long projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or replaces the donation setup for a project. Recomputes
    /// <c>DonationAmount</c> from the basis and the project's current contract value,
    /// then requires the temple split to total it exactly — otherwise throws
    /// <see cref="FluentValidation.ValidationException"/> (→ 400, key <c>temples</c>).
    /// </summary>
    Task<ProjectDonationDto> UpsertAsync(
        long projectId, UpsertProjectDonationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Re-sizes a percentage-basis donation against the project's latest contract
    /// value and flags the split for review if it no longer totals the new amount
    /// (or more has been paid than the new amount). No-op when there is no donation
    /// or the basis is Fixed. Called from <c>ProjectService.UpdateAsync</c>.
    /// </summary>
    Task RecomputeForContractValueAsync(
        long projectId, decimal newContractValue, CancellationToken cancellationToken);
}

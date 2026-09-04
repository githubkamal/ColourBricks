using ColourBricks.Application.Parties;

namespace ColourBricks.Application.Temples;

/// <summary>
/// Temples are <c>Party</c> rows that play the Temple role (BRD §26). This is a thin
/// facade over <see cref="IPartyService"/> so the temple-donation module has its own
/// permission-gated surface.
/// </summary>
public interface ITempleService
{
    Task<IReadOnlyList<PartySearchItem>> ListAsync(string? search, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartySearchItem>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    Task<PartyDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<CreatePartyResult> CreateAsync(
        string name, bool confirmed, CancellationToken cancellationToken);
}

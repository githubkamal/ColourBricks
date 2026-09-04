using ColourBricks.Application.Common.Pagination;
using ColourBricks.Domain.Parties;

namespace ColourBricks.Application.Parties;

public interface IPartyService
{
    Task<PagedResult<PartySearchItem>> ListAsync(
        PartyType? type, string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Autocomplete: ranked (exact, then prefix, then contains), capped at 20.</summary>
    Task<IReadOnlyList<PartySearchItem>> SearchAsync(
        string query, PartyType? type, int limit, CancellationToken cancellationToken);

    Task<PartyDto?> GetAsync(long id, CancellationToken cancellationToken);

    Task<CreatePartyResult> CreateAsync(
        CreatePartyRequest request, bool confirmed, CancellationToken cancellationToken);
}

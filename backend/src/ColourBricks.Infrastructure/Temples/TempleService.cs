using ColourBricks.Application.Parties;
using ColourBricks.Application.Temples;
using ColourBricks.Domain.Parties;

namespace ColourBricks.Infrastructure.Temples;

public sealed class TempleService(IPartyService parties) : ITempleService
{
    private const PartyType Role = PartyType.Temple;

    public async Task<IReadOnlyList<PartySearchItem>> ListAsync(
        string? search, CancellationToken cancellationToken)
    {
        var page = await parties.ListAsync(Role, search, page: 1, pageSize: 200, cancellationToken);
        return page.Items;
    }

    public Task<IReadOnlyList<PartySearchItem>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken) =>
        parties.SearchAsync(query, Role, limit, cancellationToken);

    public async Task<PartyDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        PartyDto? party = await parties.GetAsync(id, cancellationToken);
        return party is not null && party.Types.Contains(nameof(PartyType.Temple)) ? party : null;
    }

    public Task<CreatePartyResult> CreateAsync(
        string name, bool confirmed, CancellationToken cancellationToken) =>
        parties.CreateAsync(new CreatePartyRequest(name, [Role]), confirmed, cancellationToken);

    public Task<PartyDto?> UpdateAsync(
        long id, string name, bool isActive, string concurrencyStamp, CancellationToken cancellationToken) =>
        parties.UpdateAsync(
            id, new UpdatePartyRequest(name, [Role], isActive, concurrencyStamp), cancellationToken);
}

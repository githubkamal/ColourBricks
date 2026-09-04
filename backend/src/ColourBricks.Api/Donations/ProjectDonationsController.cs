using ColourBricks.Api.Authorization;
using ColourBricks.Application.Donations;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Donations;

[ApiController]
[Route("api/v1/projects/{projectId:long}/donation")]
public sealed class ProjectDonationsController(IProjectDonationService donations) : ControllerBase
{
    [HttpGet]
    [HasPermission("temple_donations.view")]
    public async Task<ActionResult<ProjectDonationDto>> Get(long projectId, CancellationToken cancellationToken)
    {
        ProjectDonationDto? donation = await donations.GetForProjectAsync(projectId, cancellationToken);
        return donation is null ? NotFound() : Ok(donation);
    }

    [HttpPut]
    [HasPermission("temple_donations.edit")]
    public async Task<ActionResult<ProjectDonationDto>> Upsert(
        long projectId,
        [FromBody] UpsertProjectDonationRequest request,
        CancellationToken cancellationToken)
    {
        ProjectDonationDto donation = await donations.UpsertAsync(projectId, request, cancellationToken);
        return Ok(donation);
    }
}

using ColourBricks.Api.Authorization;
using ColourBricks.Application.Donations;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Donations;

[ApiController]
[Route("api/v1/projects/{projectId:long}/donation")]
public sealed class DonationPaymentsController(IDonationPaymentService payments) : ControllerBase
{
    [HttpPost("temples/{templeId:long}/payments")]
    [HasPermission("temple_donations.edit")]
    public async Task<ActionResult<DonationPaymentDto>> Pay(
        long projectId, long templeId, [FromBody] PayDonationRequest request, CancellationToken cancellationToken)
    {
        DonationPaymentDto payment = await payments.PayAsync(projectId, templeId, request, cancellationToken);
        return Ok(payment);
    }

    [HttpGet("outstanding")]
    [HasPermission("temple_donations.view")]
    public Task<IReadOnlyList<DonationTempleOutstandingDto>> Outstanding(
        long projectId, CancellationToken cancellationToken) =>
        payments.OutstandingByTempleAsync(projectId, cancellationToken);
}

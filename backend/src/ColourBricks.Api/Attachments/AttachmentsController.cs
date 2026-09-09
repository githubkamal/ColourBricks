using System.Diagnostics;
using System.Security.Claims;
using ColourBricks.Application.Attachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Attachments;

[ApiController]
[Route("api/v1/attachments")]
[Authorize]
public sealed class AttachmentsController(IAttachmentService attachments) : ControllerBase
{
    // Each attachable record type carries the same permission as the record itself
    // (plan.md P2-T08): (view, write).
    private static readonly IReadOnlyDictionary<string, (string View, string Write)> OwnerPermissions =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["VendorPurchase"] = ("materials.view", "materials.add"),
            ["PurchaseOrder"] = ("materials.view", "materials.add"),
            ["Receipt"] = ("project_income.view", "project_income.add"),
            ["DirectExpense"] = ("project_expenses.view", "project_expenses.add"),
            ["SubcontractorWork"] = ("labour.view", "labour.edit"),
            ["TempleDonation"] = ("temple_donations.view", "temple_donations.edit"),
            ["CustomWork"] = ("customized_work.view", "customized_work.add"),
            ["Loan"] = ("loans.view", "loans.add"),
            // The company logo (the only SystemSettings attachment today) is meant to
            // show up wherever the company profile does — report headers, print views
            // — for any signed-in user, not just admins; "dashboard.view" is this
            // codebase's already-established stand-in for "any authenticated staff
            // user" (see the notification bell's gate). Uploading/replacing it stays
            // admin-only.
            ["SystemSettings"] = ("dashboard.view", "admin_configuration.edit"),
        };

    private const long MaxRequestBytes = 25 * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Upload(
        [FromForm] string ownerType,
        [FromForm] long ownerId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!OwnerPermissions.TryGetValue(ownerType, out (string View, string Write) perms))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"Unknown owner type '{ownerType}'.");
        }

        if (!HasPermission(perms.Write))
        {
            return Forbid();
        }

        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "A non-empty file is required.");
        }

        try
        {
            await using Stream stream = file.OpenReadStream();
            AttachmentDto dto = await attachments.UploadAsync(
                ownerType, ownerId, file.FileName, file.ContentType, file.Length, stream, cancellationToken);
            return CreatedAtAction(nameof(Download), new { id = dto.Id }, dto);
        }
        catch (AttachmentTooLargeException ex)
        {
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: ex.Message);
        }
        catch (AttachmentRejectedException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string ownerType, [FromQuery] long ownerId, CancellationToken cancellationToken)
    {
        if (!OwnerPermissions.TryGetValue(ownerType, out (string View, string Write) perms) || !HasPermission(perms.View))
        {
            return Forbid();
        }

        return Ok(await attachments.ListAsync(ownerType, ownerId, cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken)
    {
        (AttachmentDto Meta, Stream Content)? result = await attachments.DownloadAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        (AttachmentDto meta, Stream content) = result.Value;

        if (!OwnerPermissions.TryGetValue(meta.OwnerType, out (string View, string Write) perms)
            || !HasPermission(perms.View))
        {
            await content.DisposeAsync();
            return Forbid();
        }

        return File(content, meta.ContentType, meta.OriginalFileName);
    }

    private bool HasPermission(string permission)
    {
        string? claim = User.FindFirstValue("permissions");
        if (claim is null)
        {
            return false;
        }

        return claim == "*"
            || claim.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(permission);
    }
}

using ColourBricks.Api.Authorization;
using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Notifications;

/// <summary>
/// P9-T01 — the notification centre (BRD §66). The feed and per-user state are
/// gated on <c>dashboard.view</c>; the channel matrix and the evaluator run on
/// <c>admin_configuration.edit</c>.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(INotificationService notifications, ICurrentUser currentUser) : ControllerBase
{
    private long UserId => currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");

    [HttpGet]
    [HasPermission("dashboard.view")]
    public Task<IReadOnlyList<NotificationDto>> List(
        [FromQuery] bool unreadOnly = false, CancellationToken cancellationToken = default) =>
        notifications.ListForUserAsync(UserId, unreadOnly, cancellationToken);

    [HttpPost("{id:long}/read")]
    [HasPermission("dashboard.view")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        await notifications.MarkReadAsync(UserId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("mute")]
    [HasPermission("dashboard.view")]
    public async Task<IActionResult> Mute([FromBody] MuteTriggerRequest request, CancellationToken cancellationToken)
    {
        await notifications.MuteAsync(UserId, request.Trigger, cancellationToken);
        return NoContent();
    }

    [HttpPost("unmute")]
    [HasPermission("dashboard.view")]
    public async Task<IActionResult> Unmute([FromBody] MuteTriggerRequest request, CancellationToken cancellationToken)
    {
        await notifications.UnmuteAsync(UserId, request.Trigger, cancellationToken);
        return NoContent();
    }

    [HttpGet("config")]
    [HasPermission("admin_configuration.edit")]
    public Task<IReadOnlyList<NotificationChannelConfigDto>> Config(CancellationToken cancellationToken) =>
        notifications.GetChannelConfigAsync(cancellationToken);

    [HttpPut("config")]
    [HasPermission("admin_configuration.edit")]
    public async Task<IActionResult> SetConfig(
        [FromBody] SetNotificationChannelRequest request, CancellationToken cancellationToken)
    {
        await notifications.SetChannelConfigAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("evaluate")]
    [HasPermission("admin_configuration.edit")]
    public Task<NotificationRunResult> Evaluate(
        [FromServices] INotificationEvaluator evaluator, CancellationToken cancellationToken) =>
        evaluator.RunAsync(cancellationToken);
}

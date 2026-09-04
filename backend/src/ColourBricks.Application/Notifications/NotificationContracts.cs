namespace ColourBricks.Application.Notifications;

public sealed record NotificationDto(
    long Id,
    string Trigger,
    string Title,
    string Body,
    string Severity,
    long? ProjectId,
    string? EntityType,
    long? EntityId,
    DateTimeOffset CreatedAtUtc,
    bool Read);

public sealed record NotificationRunResult(int Created, int EmailsSent, int EmailsFailed);

public sealed record NotificationChannelConfigDto(long RoleId, string RoleName, string Trigger, string Channel);

public sealed record SetNotificationChannelRequest(long RoleId, string Trigger, string Channel);

public sealed record MuteTriggerRequest(string Trigger);

/// <summary>Sends an email. Implementations must throw on failure; the caller isolates it.</summary>
public interface IEmailSender
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>
/// Evaluates every BRD §66 trigger and raises new notifications (idempotent by
/// dedupe key), then attempts email delivery for the channels that ask for it —
/// a failed send is recorded and retried, never fatal.
/// </summary>
public interface INotificationEvaluator
{
    Task<NotificationRunResult> RunAsync(CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListForUserAsync(long userId, bool unreadOnly, CancellationToken cancellationToken);

    Task MarkReadAsync(long userId, long notificationId, CancellationToken cancellationToken);

    Task MuteAsync(long userId, string trigger, CancellationToken cancellationToken);

    Task UnmuteAsync(long userId, string trigger, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationChannelConfigDto>> GetChannelConfigAsync(CancellationToken cancellationToken);

    Task SetChannelConfigAsync(SetNotificationChannelRequest request, CancellationToken cancellationToken);
}

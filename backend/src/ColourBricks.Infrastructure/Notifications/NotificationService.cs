using ColourBricks.Application.Notifications;
using ColourBricks.Domain.Notifications;
using ColourBricks.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Notifications;

/// <summary>P9-T01 — the read side: a user's feed, read state, per-trigger mute and the per-role channel matrix.</summary>
public sealed class NotificationService(AppDbContext db, TimeProvider clock) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> ListForUserAsync(long userId, bool unreadOnly, CancellationToken ct)
    {
        long? roleId = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.RoleId).FirstOrDefaultAsync(ct);

        HashSet<string> muted = (await db.Set<NotificationMute>().AsNoTracking()
                .Where(m => m.UserId == userId).Select(m => m.Trigger).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<string, NotificationChannel> channels = roleId is { } rid
            ? await db.Set<NotificationChannelConfig>().AsNoTracking()
                .Where(c => c.RoleId == rid)
                .ToDictionaryAsync(c => c.Trigger, c => c.Channel, ct)
            : [];

        List<Notification> notifications = await db.Set<Notification>().AsNoTracking()
            .OrderByDescending(n => n.Id)
            .ToListAsync(ct);

        HashSet<long> readIds = (await db.Set<NotificationRead>().AsNoTracking()
                .Where(r => r.UserId == userId).Select(r => r.NotificationId).ToListAsync(ct))
            .ToHashSet();

        var result = new List<NotificationDto>();
        foreach (Notification n in notifications)
        {
            if (muted.Contains(n.Trigger))
            {
                continue;
            }

            NotificationChannel channel = channels.GetValueOrDefault(n.Trigger, NotificationChannel.Dashboard);
            if (channel is not (NotificationChannel.Dashboard or NotificationChannel.Both))
            {
                continue;
            }

            bool read = readIds.Contains(n.Id);
            if (unreadOnly && read)
            {
                continue;
            }

            result.Add(new NotificationDto(
                n.Id, n.Trigger, n.Title, n.Body, n.Severity, n.ProjectId, n.EntityType, n.EntityId,
                n.CreatedAtUtc, read));
        }

        return result;
    }

    public async Task MarkReadAsync(long userId, long notificationId, CancellationToken ct)
    {
        bool exists = await db.Set<NotificationRead>()
            .AnyAsync(r => r.UserId == userId && r.NotificationId == notificationId, ct);
        if (exists)
        {
            return;
        }

        db.Set<NotificationRead>().Add(new NotificationRead
        {
            UserId = userId,
            NotificationId = notificationId,
            ReadAtUtc = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task MuteAsync(long userId, string trigger, CancellationToken ct)
    {
        Require(trigger);
        bool exists = await db.Set<NotificationMute>().AnyAsync(m => m.UserId == userId && m.Trigger == trigger, ct);
        if (exists)
        {
            return;
        }

        db.Set<NotificationMute>().Add(new NotificationMute { UserId = userId, Trigger = trigger });
        await db.SaveChangesAsync(ct);
    }

    public async Task UnmuteAsync(long userId, string trigger, CancellationToken ct)
    {
        NotificationMute? row = await db.Set<NotificationMute>()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Trigger == trigger, ct);
        if (row is not null)
        {
            db.Set<NotificationMute>().Remove(row);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<NotificationChannelConfigDto>> GetChannelConfigAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().Select(r => new { r.Id, r.Name }).ToListAsync(ct);
        List<NotificationChannelConfig> configs = await db.Set<NotificationChannelConfig>().AsNoTracking().ToListAsync(ct);

        var result = new List<NotificationChannelConfigDto>();
        foreach (var role in roles)
        {
            foreach (string trigger in NotificationTrigger.All)
            {
                NotificationChannel channel = configs
                    .FirstOrDefault(c => c.RoleId == role.Id && c.Trigger == trigger)?.Channel
                    ?? NotificationChannel.Dashboard;
                result.Add(new NotificationChannelConfigDto(role.Id, role.Name, trigger, channel.ToString()));
            }
        }

        return result;
    }

    public async Task SetChannelConfigAsync(SetNotificationChannelRequest request, CancellationToken ct)
    {
        Require(request.Trigger);
        if (!Enum.TryParse(request.Channel, ignoreCase: true, out NotificationChannel channel))
        {
            throw Fail("channel", "Channel must be Off, Dashboard, Email or Both.");
        }

        if (!await db.Roles.AnyAsync(r => r.Id == request.RoleId, ct))
        {
            throw Fail("roleId", "The role does not exist.");
        }

        NotificationChannelConfig? row = await db.Set<NotificationChannelConfig>()
            .FirstOrDefaultAsync(c => c.RoleId == request.RoleId && c.Trigger == request.Trigger, ct);
        if (row is null)
        {
            db.Set<NotificationChannelConfig>().Add(new NotificationChannelConfig
            {
                RoleId = request.RoleId,
                Trigger = request.Trigger,
                Channel = channel,
            });
        }
        else
        {
            row.Channel = channel;
        }

        await db.SaveChangesAsync(ct);
    }

    private static void Require(string trigger)
    {
        if (!NotificationTrigger.IsValid(trigger))
        {
            throw Fail("trigger", $"Unknown trigger '{trigger}'.");
        }
    }

    private static ValidationException Fail(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}

/// <summary>
/// P9-T01 — SMTP email. Throws when SMTP is not configured (dev / test / a missing
/// host); the evaluator isolates that so delivery failures never block the job.
/// </summary>
public sealed class SmtpEmailSender(Microsoft.Extensions.Options.IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public Task SendAsync(string toAddress, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("Email is not configured (Email:Host is empty).");
        }

        using var client = new System.Net.Mail.SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
        };
        using var message = new System.Net.Mail.MailMessage(_options.FromAddress, toAddress, subject, body);
        return client.SendMailAsync(message, ct);
    }
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = "no-reply@colourbricks.local";
}

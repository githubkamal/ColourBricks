using ColourBricks.Application.Abstractions;
using ColourBricks.Infrastructure.Persistence;

namespace ColourBricks.Infrastructure.Auditing;

/// <summary>
/// Queues audit rows for non-CRUD actions onto the current <see cref="AppDbContext"/>
/// so they persist in the caller's transaction (plan.md §10).
/// </summary>
public sealed class AuditService(AppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
    : IAuditService
{
    public void RecordAction(string module, string action, string recordId, string? details = null)
    {
        db.Set<AuditLog>().Add(new AuditLog
        {
            UserId = currentUser.UserId,
            TimestampUtc = timeProvider.GetUtcNow(),
            Module = module,
            Action = action,
            EntityType = null,
            RecordId = recordId,
            Details = details,
        });
    }
}

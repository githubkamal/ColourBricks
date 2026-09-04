using ColourBricks.Domain.Ledger;
using ColourBricks.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ColourBricks.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Enforces the append-only rule for financial and audit records (plan.md §5.6, §10):
/// a <see cref="LedgerEntry"/> or <see cref="AuditLog"/> may only be inserted, never
/// modified or deleted.
/// </summary>
public sealed class AppendOnlyGuardInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry is { State: EntityState.Modified or EntityState.Deleted, Entity: LedgerEntry or AuditLog })
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} is append-only and cannot be "
                    + $"{entry.State.ToString().ToLowerInvariant()} (plan.md §5.6).");
            }
        }
    }
}

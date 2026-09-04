using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ColourBricks.Application.Abstractions;
using ColourBricks.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ColourBricks.Infrastructure.Auditing;

/// <summary>
/// Writes an <see cref="AuditLog"/> row for every insert, update and delete of a
/// <see cref="AuditableAttribute"/>-marked entity, serialising only the changed
/// properties (plan.md §10, BRD §65). Update/delete rows are written inside the same
/// <c>SaveChanges</c>; insert rows are written straight after, once the generated key
/// is known.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    private static readonly HashSet<string> IgnoredProperties = new(StringComparer.Ordinal)
    {
        "Id", "ConcurrencyStamp", "CreatedAtUtc", "CreatedByUserId",
        "UpdatedAtUtc", "UpdatedByUserId", "PasswordHash", "TokenHash", "ReplacedByTokenHash",
    };

    private readonly ConditionalWeakTable<DbContext, List<PendingInsert>> _pendingInserts = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FinaliseInserts(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await FinaliseInsertsAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        long? userId = currentUser.UserId;

        foreach (EntityEntry entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog)
            {
                continue;
            }

            AuditableAttribute? auditable = entry.Metadata.ClrType.GetCustomAttribute<AuditableAttribute>();
            if (auditable is null)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    _pendingInserts.GetOrCreateValue(context).Add(new PendingInsert(
                        entry, auditable.Module, now, userId, Serialize(entry, PropertyValue.Current, onlyModified: false)));
                    break;

                case EntityState.Modified:
                    context.Add(BuildLog(entry, auditable.Module, "update", now, userId,
                        oldValues: Serialize(entry, PropertyValue.Original, onlyModified: true),
                        newValues: Serialize(entry, PropertyValue.Current, onlyModified: true)));
                    break;

                case EntityState.Deleted:
                    context.Add(BuildLog(entry, auditable.Module, "delete", now, userId,
                        oldValues: Serialize(entry, PropertyValue.Current, onlyModified: false),
                        newValues: null));
                    break;

                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    break;
            }
        }
    }

    private void FinaliseInserts(DbContext? context)
    {
        if (!TryTakePending(context, out DbContext ctx, out List<PendingInsert> pending))
        {
            return;
        }

        AddInsertLogs(ctx, pending);
        ctx.SaveChanges();
    }

    private async Task FinaliseInsertsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (!TryTakePending(context, out DbContext ctx, out List<PendingInsert> pending))
        {
            return;
        }

        AddInsertLogs(ctx, pending);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    private bool TryTakePending(DbContext? context, out DbContext ctx, out List<PendingInsert> pending)
    {
        ctx = context!;
        pending = [];

        if (context is null || !_pendingInserts.TryGetValue(context, out List<PendingInsert>? list) || list.Count == 0)
        {
            return false;
        }

        _pendingInserts.Remove(context); // remove before re-saving to avoid re-entrancy
        pending = list;
        return true;
    }

    private static void AddInsertLogs(DbContext context, List<PendingInsert> pending)
    {
        foreach (PendingInsert insert in pending)
        {
            context.Add(new AuditLog
            {
                UserId = insert.UserId,
                TimestampUtc = insert.Timestamp,
                Module = insert.Module,
                Action = "create",
                EntityType = insert.Entry.Metadata.ClrType.Name,
                RecordId = KeyString(insert.Entry),
                OldValues = null,
                NewValues = insert.NewValues,
            });
        }
    }

    private static AuditLog BuildLog(
        EntityEntry entry, string module, string action, DateTimeOffset now, long? userId,
        string? oldValues, string? newValues) => new()
    {
        UserId = userId,
        TimestampUtc = now,
        Module = module,
        Action = action,
        EntityType = entry.Metadata.ClrType.Name,
        RecordId = KeyString(entry),
        OldValues = oldValues,
        NewValues = newValues,
    };

    private static string Serialize(EntityEntry entry, PropertyValue which, bool onlyModified)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (PropertyEntry property in entry.Properties)
        {
            string name = property.Metadata.Name;
            if (IgnoredProperties.Contains(name) || (onlyModified && !property.IsModified))
            {
                continue;
            }

            values[name] = which == PropertyValue.Original ? property.OriginalValue : property.CurrentValue;
        }

        return JsonSerializer.Serialize(values);
    }

    private static string KeyString(EntityEntry entry)
    {
        IReadOnlyList<PropertyEntry> keyProperties = entry.Metadata.FindPrimaryKey()!.Properties
            .Select(p => entry.Property(p.Name))
            .ToList();

        return string.Join(',', keyProperties.Select(p => p.CurrentValue));
    }

    private enum PropertyValue
    {
        Current,
        Original,
    }

    private sealed record PendingInsert(
        EntityEntry Entry, string Module, DateTimeOffset Timestamp, long? UserId, string NewValues);
}

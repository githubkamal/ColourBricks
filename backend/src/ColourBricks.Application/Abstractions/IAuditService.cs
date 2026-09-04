namespace ColourBricks.Application.Abstractions;

/// <summary>
/// Records audit-trail entries for actions that are not simple property changes —
/// reconcile, unreconcile, exclude, allocation override, permission change
/// (plan.md §10, BRD §65). Simple CRUD is captured automatically by the interceptor.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Queues an audit row on the current unit of work. It is persisted with the
    /// caller's next <c>SaveChanges</c>, in the same transaction.
    /// </summary>
    void RecordAction(string module, string action, string recordId, string? details = null);
}

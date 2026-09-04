using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ColourBricks.Infrastructure.Diagnostics;

/// <summary>
/// P9-T02 — counts EF Core database round-trips process-wide. Integration tests run
/// serially, so <see cref="Reset"/> then one request then <see cref="Count"/> is a
/// reliable N+1 guard.
/// </summary>
public sealed class QueryCounter
{
    private long _count;

    public long Count => Interlocked.Read(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    internal void Increment() => Interlocked.Increment(ref _count);
}

/// <summary>Increments <see cref="QueryCounter"/> on every executed command.</summary>
public sealed class QueryCountInterceptor(QueryCounter counter) : DbCommandInterceptor
{
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        counter.Increment();
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        counter.Increment();
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        counter.Increment();
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        counter.Increment();
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        counter.Increment();
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        counter.Increment();
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }
}

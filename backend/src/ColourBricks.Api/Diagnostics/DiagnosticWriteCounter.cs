namespace ColourBricks.Api.Diagnostics;

/// <summary>
/// Process-wide counter incremented by the diagnostics "idempotent write" endpoint.
/// Lets the idempotency integration test prove the handler body ran exactly once
/// across a replayed <c>Idempotency-Key</c>. Not used by real features.
/// </summary>
public sealed class DiagnosticWriteCounter
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public int Increment() => Interlocked.Increment(ref _count);

    public void Reset() => Volatile.Write(ref _count, 0);
}

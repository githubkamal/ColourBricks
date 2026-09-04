using ColourBricks.Domain.Common;

namespace ColourBricks.Infrastructure.Idempotency;

/// <summary>
/// One row per <c>Idempotency-Key</c> seen on a write endpoint marked
/// <c>[Idempotent]</c> (plan.md §7). A replay within the 24-hour window
/// (measured from <see cref="BaseEntity.CreatedAtUtc"/>) returns the stored
/// response instead of running the handler again.
/// </summary>
public sealed class IdempotencyRecord : BaseEntity
{
    /// <summary>The client-supplied key. Unique.</summary>
    public required string Key { get; set; }

    public required string Method { get; set; }

    public required string Path { get; set; }

    /// <summary>Null while the first request is still in flight.</summary>
    public int? StatusCode { get; set; }

    public string? ContentType { get; set; }

    public string? ResponseBody { get; set; }
}

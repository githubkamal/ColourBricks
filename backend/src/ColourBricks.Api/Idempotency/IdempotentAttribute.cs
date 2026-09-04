namespace ColourBricks.Api.Idempotency;

/// <summary>
/// Marks a write endpoint as requiring an <c>Idempotency-Key</c> header. The
/// <see cref="IdempotencyMiddleware"/> reads this from endpoint metadata, so it
/// works on an action method or a whole controller (plan.md §7).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class IdempotentAttribute : Attribute;

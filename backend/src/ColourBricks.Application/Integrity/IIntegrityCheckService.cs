namespace ColourBricks.Application.Integrity;

/// <summary>One record that fails a control, with the two figures that disagree.</summary>
public sealed record IntegrityViolation(
    string Entity,
    long Id,
    string Name,
    decimal Expected,
    decimal Actual,
    string Detail);

/// <summary>The outcome of one of BRD §38's five controls.</summary>
public sealed record IntegrityControlResult(
    string Control,
    string Formula,
    bool Passed,
    IReadOnlyList<IntegrityViolation> Violations);

/// <summary>The full BRD §38 control-report run (plan.md §11 safety net).</summary>
public sealed record IntegrityCheckReport(
    bool Passed,
    DateTimeOffset RunAtUtc,
    IReadOnlyList<IntegrityControlResult> Controls);

public interface IIntegrityCheckService
{
    /// <summary>Runs all five controls over the whole database. Cheap enough for an admin request at this scale.</summary>
    Task<IntegrityCheckReport> RunAsync(CancellationToken cancellationToken);
}

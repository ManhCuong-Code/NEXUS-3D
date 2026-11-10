using NEXUS3D.Conversion.Abstractions.Models;

namespace NEXUS3D.Conversion.Domain;

/// <summary>
/// Domain model encapsulating performance metrics and execution timestamps of a conversion pipeline.
/// </summary>
public sealed class ConversionAuditTrail
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public long DurationMs { get; private set; }
    public List<string> ExecutionSteps { get; } = [];
    public List<ConversionLossItem> RecordedLossItems { get; } = [];

    public void RecordStep(string stepName, long stepDurationMs)
    {
        ExecutionSteps.Add($"[{stepDurationMs}ms] {stepName}");
    }

    public void Complete(long totalDurationMs)
    {
        DurationMs = totalDurationMs;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }
}

using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Engine.Contracts;

/// <summary>
/// Represents the current readiness state of the 3D processing engine.
/// </summary>
public sealed record EngineStatusSnapshot(
    string EngineName,
    string EngineVersion,
    bool IsReady,
    int MaxConcurrentJobs,
    IReadOnlyList<ModelFormatType> SupportedPipelines);

/// <summary>
/// Foundational contract for the 3D conversion pipeline orchestrator.
/// Concrete 3D conversion algorithms will be implemented in later stages.
/// </summary>
public interface IConversionPipeline
{
    EngineStatusSnapshot GetStatusSnapshot();
    Task<OperationResult> ValidateCompatibilityAsync(
        ConversionProfile profile,
        CancellationToken cancellationToken = default);
}


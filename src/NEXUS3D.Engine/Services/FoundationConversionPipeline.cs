using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Rules;
using NEXUS3D.Engine.Contracts;
using NEXUS3D.Shared.Constants;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Engine.Services;

/// <summary>
/// Foundation stage implementation of <see cref="IConversionPipeline"/>.
/// Validates pipeline parameters and reports engine capabilities without executing 3D geometry conversion.
/// </summary>
public sealed class FoundationConversionPipeline : IConversionPipeline
{
    private static readonly IReadOnlyList<ModelFormatType> SupportedFormats =
    [
        ModelFormatType.Schematic,
        ModelFormatType.Obj,
        ModelFormatType.VoxelJson,
        ModelFormatType.BedrockGeometry
    ];

    public EngineStatusSnapshot GetStatusSnapshot() =>
        new(
            EngineName: "NEXUS 3D Geometry Core",
            EngineVersion: AppConstants.Version,
            IsReady: true,
            MaxConcurrentJobs: Math.Max(1, Environment.ProcessorCount / 2),
            SupportedPipelines: SupportedFormats);

    public Task<OperationResult> ValidateCompatibilityAsync(
        ConversionProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        OperationResult validation = ConversionDomainRules.ValidateProfile(profile);
        if (validation.IsFailure)
        {
            return Task.FromResult(validation);
        }

        return Task.FromResult(OperationResult.Success());
    }
}


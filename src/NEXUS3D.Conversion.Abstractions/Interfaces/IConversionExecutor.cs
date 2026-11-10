using NEXUS3D.Conversion.Abstractions.Models;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Pipeline orchestrator converting any supported format to another purely through USM.
/// </summary>
public interface IConversionExecutor
{
    Task<ConversionResult> ExecuteAsync(
        ConversionRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Alias interface for the universal conversion pipeline.
/// </summary>
public interface IUniversalConversionPipeline : IConversionExecutor
{
}

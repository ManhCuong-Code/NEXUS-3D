using NEXUS3D.Conversion.Abstractions.Models;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Service managing bounded-concurrency batch execution across multiple conversion requests.
/// </summary>
public interface IBatchConversionService
{
    Task<BatchConversionResult> ConvertBatchAsync(
        BatchConversionRequest request,
        IProgress<BatchProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}

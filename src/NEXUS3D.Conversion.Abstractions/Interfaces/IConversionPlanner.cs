using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Service planning the optimal execution pipeline between two formats via USM.
/// </summary>
public interface IConversionPlanner
{
    Task<OperationResult<ConversionPlan>> CreatePlanAsync(
        ConversionRequest request,
        Scene3D? preloadedScene = null,
        CancellationToken cancellationToken = default);
}

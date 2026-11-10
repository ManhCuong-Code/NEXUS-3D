using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Service adapting materials, shaders, textures, and paths between format standards.
/// </summary>
public interface IMaterialMappingService
{
    Task<OperationResult> MapMaterialsAsync(
        Scene3D scene,
        ConversionRequest request,
        CancellationToken cancellationToken = default);
}

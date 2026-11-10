using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Converts Bedrock cuboid bones and hierarchies into static scene meshes.
/// </summary>
public interface IBedrockCubeToMeshConverter
{
    OperationResult<MeshData> ConvertBedrockCubesToMesh(
        Scene3D bedrockScene,
        BedrockCubeToMeshOptions? options = null);
}

using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Converts volumetric VoxelGrid into surface polygon meshes using greedy meshing or exposed-face culling.
/// </summary>
public interface IVoxelToMeshConverter
{
    OperationResult<MeshData> ConvertVoxelGridToMesh(
        VoxelGrid grid,
        VoxelToMeshOptions? options = null);
}

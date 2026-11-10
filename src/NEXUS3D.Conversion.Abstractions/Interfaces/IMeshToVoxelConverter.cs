using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Converts polygon mesh data into volumetric voxel grids using 3D rasterization.
/// </summary>
public interface IMeshToVoxelConverter
{
    OperationResult<VoxelGrid> ConvertMeshToVoxelGrid(
        MeshData mesh,
        Scene3D? parentScene = null,
        MeshToVoxelOptions? options = null);
}

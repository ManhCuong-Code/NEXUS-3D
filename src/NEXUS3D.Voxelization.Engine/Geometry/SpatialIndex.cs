using System.Numerics;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Voxelization.Engine.Processing;

namespace NEXUS3D.Voxelization.Engine.Geometry;

/// <summary>
/// Fast discrete bounding box mapping to restrict triangle-voxel intersection tests only to proximate candidate cells.
/// </summary>
public static class SpatialIndex
{
    public static (VoxelPosition Min, VoxelPosition Max) GetVoxelRange(
        WorkingTriangle triangle,
        Vector3 origin,
        float voxelSize,
        int dimX,
        int dimY,
        int dimZ,
        float expansion = 0.0f)
    {
        var (tMin, tMax) = triangle.Bounds;
        tMin -= new Vector3(expansion);
        tMax += new Vector3(expansion);

        int minX = Math.Clamp((int)MathF.Floor((tMin.X - origin.X) / voxelSize), 0, dimX - 1);
        int maxX = Math.Clamp((int)MathF.Floor((tMax.X - origin.X) / voxelSize), 0, dimX - 1);

        int minY = Math.Clamp((int)MathF.Floor((tMin.Y - origin.Y) / voxelSize), 0, dimY - 1);
        int maxY = Math.Clamp((int)MathF.Floor((tMax.Y - origin.Y) / voxelSize), 0, dimY - 1);

        int minZ = Math.Clamp((int)MathF.Floor((tMin.Z - origin.Z) / voxelSize), 0, dimZ - 1);
        int maxZ = Math.Clamp((int)MathF.Floor((tMax.Z - origin.Z) / voxelSize), 0, dimZ - 1);

        return (new VoxelPosition(minX, minY, minZ), new VoxelPosition(maxX, maxY, maxZ));
    }
}

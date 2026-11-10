using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Voxelization.Abstractions.Models;

/// <summary>
/// Axis-Aligned Bounding Box representation of merged contiguous voxels sharing the same material state.
/// </summary>
public sealed class VoxelCuboid
{
    public VoxelPosition MinPosition { get; init; }
    public VoxelPosition MaxPosition { get; init; }
    public int PaletteIndex { get; init; }
    public Guid? MaterialId { get; init; }
    public BlockState State { get; init; } = new();

    public int SizeX => MaxPosition.X - MinPosition.X + 1;
    public int SizeY => MaxPosition.Y - MinPosition.Y + 1;
    public int SizeZ => MaxPosition.Z - MinPosition.Z + 1;
    public int Volume => SizeX * SizeY * SizeZ;

    public VoxelCuboid(
        VoxelPosition minPosition,
        VoxelPosition maxPosition,
        int paletteIndex,
        BlockState state,
        Guid? materialId = null)
    {
        MinPosition = minPosition;
        MaxPosition = maxPosition;
        PaletteIndex = paletteIndex;
        State = state;
        MaterialId = materialId;
    }

    public BoundingBox ToBoundingBox(VoxelSize voxelSize)
    {
        var min = new Vector3D(MinPosition.X * voxelSize.X, MinPosition.Y * voxelSize.Y, MinPosition.Z * voxelSize.Z);
        var max = new Vector3D((MaxPosition.X + 1) * voxelSize.X, (MaxPosition.Y + 1) * voxelSize.Y, (MaxPosition.Z + 1) * voxelSize.Z);
        return new BoundingBox(min, max);
    }
}

/// <summary>
/// Result summary of greedy cuboid optimization.
/// </summary>
public sealed record CuboidOptimizationResult(
    int OriginalVoxelCount,
    int CuboidCount,
    IReadOnlyList<VoxelCuboid> Cuboids,
    double ReductionPercentage,
    long ElapsedMilliseconds);

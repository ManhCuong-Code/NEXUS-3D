using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Models;

namespace NEXUS3D.Formats.VoxelJson.Exporting;

/// <summary>
/// Options controlling Voxel JSON serialization and dialect output.
/// </summary>
public sealed record VoxelJsonWriteOptions : JsonWriteOptions
{
    public static new VoxelJsonWriteOptions Default { get; } = new();

    /// <summary>
    /// Optional target dialect override. When <see langword="null"/>, exports using <see cref="VoxelDocument.Dialect"/>
    /// (falling back to <see cref="VoxelJsonDialect.NexusVoxelGridV1"/>).
    /// </summary>
    public VoxelJsonDialect? TargetDialect { get; init; }

    /// <summary>
    /// When exporting <see cref="VoxelJsonDialect.SparseVoxelArray"/>, whether to emit compact <c>[x, y, z, color]</c>
    /// tuples for simple 1x1x1 unrotated voxels.
    /// </summary>
    public bool WriteCompactVoxelTuples { get; init; }
}

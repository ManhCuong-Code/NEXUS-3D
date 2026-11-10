namespace NEXUS3D.Voxelization.Abstractions.Models;

public enum VoxelizationWarningSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public sealed record VoxelizationWarning(
    string Code,
    string Message,
    VoxelizationWarningSeverity Severity = VoxelizationWarningSeverity.Warning)
{
    public static VoxelizationWarning OpenMeshNonManifold(string meshName) =>
        new("VOX_OPEN_MESH", $"Mesh '{meshName}' is open or non-manifold; solid interior flood-fill might produce incomplete results.", VoxelizationWarningSeverity.Warning);

    public static VoxelizationWarning DegenerateTrianglesSkipped(int count, string meshName) =>
        new("VOX_DEGENERATE_TRIANGLES", $"Skipped {count} degenerate or zero-area triangles in mesh '{meshName}'.", VoxelizationWarningSeverity.Info);

    public static VoxelizationWarning TextureMissing(string texturePath) =>
        new("VOX_TEXTURE_MISSING", $"Texture '{texturePath}' could not be located; falling back to diffuse/vertex color.", VoxelizationWarningSeverity.Warning);

    public static VoxelizationWarning HighVoxelCountWarning(long estimatedCount, long maxAllowed) =>
        new("VOX_HIGH_DENSITY", $"Requested resolution produces ~{estimatedCount:N0} voxels (cap: {maxAllowed:N0}); processing clamped.", VoxelizationWarningSeverity.Warning);
}

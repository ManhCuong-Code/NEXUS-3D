using NEXUS3D.Formats.VoxelJson.Exporting;
using NEXUS3D.Formats.VoxelJson.Models;

namespace NEXUS3D.Formats.VoxelJson.Conversion;

/// <summary>
/// Options controlling bidirectional mapping between <see cref="VoxelDocument"/> and USM <see cref="NEXUS3D.Domain.Scene.Scene3D"/>.
/// </summary>
public sealed record VoxelMappingOptions
{
    public static VoxelMappingOptions Default { get; } = new();

    public bool StrictMode { get; init; }
    public bool PreserveRawData { get; init; } = true;
    public DuplicateVoxelPolicy DuplicatePolicy { get; init; } = DuplicateVoxelPolicy.KeepLastWithWarning;
    public VoxelJsonDialect? TargetDialect { get; init; }
    public string? SourceFileName { get; init; }
    public long SourceFileSizeBytes { get; init; }
    public VoxelJsonWriteOptions ExportOptions { get; init; } = VoxelJsonWriteOptions.Default;
}

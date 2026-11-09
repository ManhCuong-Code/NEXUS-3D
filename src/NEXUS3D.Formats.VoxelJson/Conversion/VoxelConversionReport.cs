using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.VoxelJson.Models;

namespace NEXUS3D.Formats.VoxelJson.Conversion;

/// <summary>
/// Detailed report of a conversion between Voxel JSON and USM <see cref="NEXUS3D.Domain.Scene.Scene3D"/>.
/// </summary>
public sealed class VoxelConversionReport
{
    public VoxelJsonDialect Dialect { get; init; }
    public int VoxelCount { get; init; }
    public int MaterialCount { get; init; }
    public int LayerCount { get; init; }
    public int PreservedExtensionCount { get; init; }
    public LossReport LossReport { get; init; } = new();
    public List<WarningRecord> Warnings { get; init; } = [];
    public List<ValidationIssue> Issues { get; init; } = [];
}

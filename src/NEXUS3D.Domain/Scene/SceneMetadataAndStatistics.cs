using System.Text.Json;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Domain.Scene;

/// <summary>
/// Scene-level metadata, provenance history, preserved raw data, extension bags, warnings, and loss report.
/// </summary>
public sealed class SceneMetadata : IDeepCloneable<SceneMetadata>
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public SourceMetadata Source { get; set; } = new();
    public List<FormatMetadata> Formats { get; set; } = [];
    public Dictionary<string, ExtensionData> Extensions { get; set; } = new(StringComparer.Ordinal);
    public List<RawDataContainer> PreservedRawData { get; set; } = [];
    public List<DataProvenance> ProvenanceHistory { get; set; } = [];
    public List<WarningRecord> Warnings { get; set; } = [];
    public LossReport LossReport { get; set; } = new();

    public void AddExtension(ExtensionData extension)
    {
        DomainGuard.NotNull(extension);
        if (!extension.IsValidNamespace)
        {
            throw new DomainException(
                UsmErrorCodes.InvalidMetadata,
                $"Extension namespace '{extension.Namespace}' must follow 'prefix:name' convention.");
        }

        Extensions[extension.Namespace] = extension;
    }

    public SceneMetadata DeepClone()
    {
        var clone = new SceneMetadata
        {
            Title = Title,
            Author = Author,
            Description = Description,
            Tags = Tags.ToList(),
            Source = Source.DeepClone(),
            Formats = Formats.Select(f => f.DeepClone()).ToList(),
            PreservedRawData = PreservedRawData.Select(r => r.DeepClone()).ToList(),
            ProvenanceHistory = ProvenanceHistory.Select(p => p.DeepClone()).ToList(),
            Warnings = Warnings.Select(w => w.DeepClone()).ToList(),
            LossReport = LossReport.DeepClone()
        };

        foreach (var kvp in Extensions)
        {
            clone.Extensions[kvp.Key] = kvp.Value.DeepClone();
        }

        return clone;
    }
}

/// <summary>
/// Configurable workspace and inspection settings attached to a <see cref="Scene3D"/>.
/// </summary>
public sealed class SceneSettings : IDeepCloneable<SceneSettings>
{
    public double GridCellSize { get; set; } = 1.0;
    public bool SnapToGrid { get; set; } = true;
    public bool StrictValidation { get; set; } = true;
    public bool PreserveUnmappedFields { get; set; } = true;

    public SceneSettings DeepClone() => new()
    {
        GridCellSize = GridCellSize,
        SnapToGrid = SnapToGrid,
        StrictValidation = StrictValidation,
        PreserveUnmappedFields = PreserveUnmappedFields
    };
}

/// <summary>
/// Comprehensive structural and geometric statistics of a <see cref="Scene3D"/>.
/// </summary>
public sealed record SceneStatistics(
    int NodeCount,
    int MaxHierarchyDepth,
    int SceneObjectCount,
    int MeshCount,
    int TotalVertexCount,
    int TotalFaceCount,
    int VoxelGridCount,
    int TotalVoxelCount,
    int MaterialCount,
    int TextureCount,
    int SkeletonCount,
    int TotalBoneCount,
    int AnimationCount,
    int PreservedRawDataCount,
    BoundingBox WorldBounds);


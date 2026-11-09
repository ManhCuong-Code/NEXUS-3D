using NEXUS3D.Domain.Animations;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Domain.Textures;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Domain.Scene;

/// <summary>
/// Immutable point-in-time snapshot of a <see cref="Scene3D"/> preserving all original identifiers for undo/redo and comparison.
/// </summary>
public sealed class SceneSnapshot
{
    public Guid SnapshotId { get; init; } = Guid.NewGuid();
    public Guid SceneId { get; init; }
    public string Label { get; init; } = "Snapshot";
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public Scene3D SceneState { get; init; }

    public SceneSnapshot(Scene3D sceneState, string? label = null)
    {
        DomainGuard.NotNull(sceneState);
        SceneId = sceneState.Id;
        Label = string.IsNullOrWhiteSpace(label) ? $"Snapshot @ {DateTimeOffset.UtcNow:HH:mm:ss}" : label;
        SceneState = sceneState.DeepClone(preserveIds: true);
    }

    public Scene3D Restore() => SceneState.DeepClone(preserveIds: true);
}

/// <summary>
/// Root aggregate of the Universal Scene Model (USM).
/// Format-agnostic representation of a complete 3D scene including SceneGraph, Meshes, VoxelGrids,
/// Materials, Textures, Skeletons, Animations, CoordinateMetadata, and Preserved Raw Data.
/// </summary>
public sealed class Scene3D : Entity, ISceneComponent, IDeepCloneable<Scene3D>
{
    public const int CurrentSchemaVersion = 1;

    public string Name { get; set; } = "Untitled Scene";
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public SceneGraph Graph { get; set; } = SceneGraph.CreateDefault();
    public SceneMetadata Metadata { get; set; } = new();
    public SceneSettings Settings { get; set; } = new();
    public CoordinateMetadata Coordinates { get; set; } = CoordinateMetadata.CreateDefault();
    public MaterialLibrary Materials { get; set; } = new();
    public List<TextureData> Textures { get; set; } = [];
    public List<SkeletonData> Skeletons { get; set; } = [];
    public List<AnimationData> Animations { get; set; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    public SceneNode? FindNode(Guid nodeId) => Graph?.FindNode(nodeId);

    public bool ContainsNode(Guid nodeId) => Graph?.FindNode(nodeId) is not null;

    public ValidationResult Validate() => new SceneValidator().Validate(this);

    /// <summary>
    /// Creates an undo/redo snapshot that strictly preserves all entity <see cref="Guid"/> identifiers.
    /// </summary>
    public SceneSnapshot CreateSnapshot(string? label = null) =>
        new(this, label);

    /// <summary>
    /// Creates an independent deep clone of the entire scene.
    /// When <paramref name="preserveIds"/> is true (default), all original entity GUIDs are preserved.
    /// </summary>
    public Scene3D DeepClone() => DeepClone(preserveIds: true);

    public Scene3D DeepClone(bool preserveIds)
    {
        var clone = new Scene3D
        {
            Id = preserveIds ? Id : Guid.NewGuid(),
            Name = Name,
            SchemaVersion = SchemaVersion,
            Graph = Graph.DeepClone(),
            Metadata = Metadata.DeepClone(),
            Settings = Settings.DeepClone(),
            Coordinates = Coordinates.DeepClone(),
            Materials = Materials.DeepClone(),
            Textures = Textures.Select(t => t.DeepClone()).ToList(),
            Skeletons = Skeletons.Select(s => s.DeepClone()).ToList(),
            Animations = Animations.Select(a => a.DeepClone()).ToList(),
            CreatedAt = CreatedAt,
            ModifiedAt = ModifiedAt
        };

        return clone;
    }

    public SceneStatistics CalculateStatistics()
    {
        int nodeCount = 0;
        int objectCount = 0;
        int meshCount = 0;
        int vertexCount = 0;
        int faceCount = 0;
        int voxelGridCount = 0;
        int voxelCount = 0;

        foreach (var node in Graph.Traverse())
        {
            nodeCount++;
            if (node.Object is { } obj)
            {
                objectCount++;
                meshCount += obj.Meshes.Count;
                for (int i = 0; i < obj.Meshes.Count; i++)
                {
                    vertexCount += obj.Meshes[i].Vertices.Count;
                    faceCount += obj.Meshes[i].Faces.Count;
                }

                voxelGridCount += obj.VoxelGrids.Count;
                for (int i = 0; i < obj.VoxelGrids.Count; i++)
                {
                    voxelCount += obj.VoxelGrids[i].GetVoxelCount();
                }
            }
        }

        int totalBones = Skeletons.Sum(s => s.Bones.Count);
        BoundingBox worldBounds = Graph.Root.GetWorldBounds() ?? BoundingBox.Empty;

        return new SceneStatistics(
            NodeCount: nodeCount,
            MaxHierarchyDepth: Graph.ComputeMaxDepth(),
            SceneObjectCount: objectCount,
            MeshCount: meshCount,
            TotalVertexCount: vertexCount,
            TotalFaceCount: faceCount,
            VoxelGridCount: voxelGridCount,
            TotalVoxelCount: voxelCount,
            MaterialCount: Materials.Count,
            TextureCount: Textures.Count,
            SkeletonCount: Skeletons.Count,
            TotalBoneCount: totalBones,
            AnimationCount: Animations.Count,
            PreservedRawDataCount: Metadata.PreservedRawData.Count,
            WorldBounds: worldBounds);
    }

    public void UpdateModifiedTime()
    {
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}


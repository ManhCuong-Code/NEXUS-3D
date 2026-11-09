using System.Text.Json;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Domain.Scene;

/// <summary>
/// Geometric payload attached to a <see cref="SceneNode"/>.
/// Can hold Meshes, VoxelGrids, or both, without auto-converting between them in the Domain layer.
/// </summary>
public sealed class SceneObject : Entity, ISceneComponent, IDeepCloneable<SceneObject>
{
    public string Name { get; set; } = "SceneObject";
    public List<MeshData> Meshes { get; set; } = [];
    public List<VoxelGrid> VoxelGrids { get; set; } = [];
    public Guid? SkeletonId { get; set; }
    public Dictionary<string, JsonElement> Properties { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ExtensionData> Extensions { get; set; } = new(StringComparer.Ordinal);

    public SceneObject()
    {
    }

    public SceneObject(string name)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
    }

    public BoundingBox? GetLocalBounds()
    {
        BoundingBox combined = BoundingBox.Empty;

        for (int i = 0; i < Meshes.Count; i++)
        {
            BoundingBox meshBounds = Meshes[i].Bounds;
            if (meshBounds.IsValid)
            {
                combined = combined.Encapsulate(meshBounds);
            }
        }

        for (int i = 0; i < VoxelGrids.Count; i++)
        {
            BoundingBox voxelBounds = VoxelGrids[i].GetWorldBounds();
            if (voxelBounds.IsValid)
            {
                combined = combined.Encapsulate(voxelBounds);
            }
        }

        return combined.IsValid ? combined : null;
    }

    public SceneObject DeepClone()
    {
        var clone = new SceneObject(Name)
        {
            Id = Id,
            SkeletonId = SkeletonId,
            Meshes = Meshes.Select(m => m.DeepClone()).ToList(),
            VoxelGrids = VoxelGrids.Select(v => v.DeepClone()).ToList()
        };

        foreach (var kvp in Properties)
        {
            clone.Properties[kvp.Key] = kvp.Value.Clone();
        }

        foreach (var kvp in Extensions)
        {
            clone.Extensions[kvp.Key] = kvp.Value.DeepClone();
        }

        return clone;
    }
}


using System.Text.Json;
using NEXUS3D.Domain.Animations;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Domain.Textures;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Infrastructure.Serialization.Json;

internal sealed class UsmSceneEnvelopeDto
{
    public string FormatIdentifier { get; set; } = "NEXUS3D.USM";
    public int SchemaVersion { get; set; } = Scene3D.CurrentSchemaVersion;
    public Guid SceneId { get; set; }
    public string Name { get; set; } = "Untitled Scene";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public SceneMetadata Metadata { get; set; } = new();
    public SceneSettings Settings { get; set; } = new();
    public CoordinateMetadata Coordinates { get; set; } = CoordinateMetadata.CreateDefault();
    public Guid RootNodeId { get; set; }
    public List<UsmSceneNodeDto> Nodes { get; set; } = [];
    public List<MaterialData> Materials { get; set; } = [];
    public List<TextureData> Textures { get; set; } = [];
    public List<SkeletonData> Skeletons { get; set; } = [];
    public List<AnimationData> Animations { get; set; } = [];
}

internal sealed class UsmSceneNodeDto
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "Node";
    public Transform3D LocalTransform { get; set; } = Transform3D.Identity();
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public List<Guid> ChildIds { get; set; } = [];
    public UsmSceneObjectDto? Object { get; set; }
}

internal sealed class UsmSceneObjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "SceneObject";
    public List<UsmMeshDataDto> Meshes { get; set; } = [];
    public List<UsmVoxelGridDto> VoxelGrids { get; set; } = [];
    public Guid? SkeletonId { get; set; }
    public Dictionary<string, JsonElement> Properties { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ExtensionData> Extensions { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class UsmMeshDataDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Mesh";
    public MeshTopology Topology { get; set; } = MeshTopology.Polygons;
    public List<Vertex> Vertices { get; set; } = [];
    public List<UsmFaceDto> Faces { get; set; } = [];
    public List<MeshSubmesh> Submeshes { get; set; } = [];
}

internal sealed class UsmFaceDto
{
    public int[] VertexIndices { get; set; } = [];
    public Guid? MaterialId { get; set; }
    public int? SmoothingGroup { get; set; }
}

internal sealed class UsmVoxelGridDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "VoxelGrid";
    public VoxelPosition Origin { get; set; } = VoxelPosition.Zero;
    public VoxelSize VoxelSize { get; set; } = VoxelSize.UnitCube;
    public VoxelGridBounds Bounds { get; set; } = VoxelGridBounds.Empty;
    public List<VoxelPaletteEntry> PaletteEntries { get; set; } = [];
    public List<Voxel> Voxels { get; set; } = [];
    public List<VoxelLayer> Layers { get; set; } = [];
}

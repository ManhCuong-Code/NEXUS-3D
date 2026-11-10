using System.Collections.ObjectModel;
using System.Numerics;
using NEXUS3D.Rendering.Contracts;

namespace NEXUS3D.Rendering.Domain;

/// <summary>
/// Immutable snapshot representing the active camera view, target, and projection parameters.
/// </summary>
public sealed record CameraState(
    Vector3 Position,
    Vector3 Target,
    Vector3 Up,
    CameraProjectionType ProjectionType = CameraProjectionType.Perspective,
    float FieldOfView = 45f,
    float NearPlane = 0.01f,
    float FarPlane = 1000f,
    float OrthographicWidth = 20f)
{
    public static CameraState Default => new(
        Position: new Vector3(16f, 16f, 24f),
        Target: Vector3.Zero,
        Up: Vector3.UnitY,
        ProjectionType: CameraProjectionType.Perspective,
        FieldOfView: 45f,
        NearPlane: 0.05f,
        FarPlane: 2000f,
        OrthographicWidth: 32f);

    public Vector3 LookDirection => Target - Position;
    public float DistanceToTarget => Math.Max(0.001f, Vector3.Distance(Position, Target));
}

/// <summary>
/// Visualization and shading settings for the 3D viewport.
/// </summary>
public sealed record RenderSettings(
    ShadingMode Shading = ShadingMode.Textured,
    bool ShowGrid = true,
    bool ShowAxes = true,
    bool ShowBoundingBox = false,
    bool ShowNormals = false,
    bool ShowWireframe = false,
    bool ShowBones = true,
    bool ShowTextures = true,
    bool EnableLighting = true,
    string BackgroundColorHex = "#1E1E24",
    float NormalScale = 0.5f,
    float LightIntensity = 1.0f)
{
    public static RenderSettings Default => new();
}

/// <summary>
/// GPU-ready material representation for 3D rendering.
/// </summary>
public sealed record RenderMaterial(
    string Id,
    string Name,
    Vector4 BaseColor,
    Vector4 SpecularColor,
    float Shininess = 20f,
    float Opacity = 1.0f,
    bool IsDoubleSided = false,
    string? DiffuseTexturePath = null,
    string? NormalTexturePath = null,
    string? EmissiveTexturePath = null)
{
    public static RenderMaterial Default => new(
        Id: "default",
        Name: "Default Material",
        BaseColor: new Vector4(0.8f, 0.8f, 0.85f, 1.0f),
        SpecularColor: new Vector4(0.2f, 0.2f, 0.2f, 1.0f),
        Shininess: 20f,
        Opacity: 1.0f,
        IsDoubleSided: true);
}

/// <summary>
/// Triangle-based geometry ready for GPU buffer creation.
/// </summary>
public sealed class RenderMesh
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Mesh";
    public Vector3[] Positions { get; init; } = [];
    public Vector3[] Normals { get; init; } = [];
    public Vector2[] UVs { get; init; } = [];
    public Vector4[] Colors { get; init; } = [];
    public int[] Indices { get; init; } = [];
    public int[] WireframeIndices { get; init; } = [];
    public string? MaterialId { get; init; }
    public Vector3 BoundsMin { get; init; }
    public Vector3 BoundsMax { get; init; }

    public int TriangleCount => Indices.Length / 3;
    public int VertexCount => Positions.Length;
    public Vector3 BoundsCenter => (BoundsMin + BoundsMax) * 0.5f;
    public Vector3 BoundsSize => BoundsMax - BoundsMin;
}

/// <summary>
/// Represents a voxelized chunk or collection ready for rendering.
/// </summary>
public sealed class RenderVoxelChunk
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public Vector3 Position { get; init; }
    public int VoxelCount { get; init; }
    public RenderMesh? Mesh { get; init; }
}

/// <summary>
/// Bone representation for skeleton hierarchy visualization.
/// </summary>
public sealed class RenderBone
{
    public string Name { get; init; } = string.Empty;
    public string? ParentName { get; init; }
    public Vector3 Pivot { get; init; }
    public Vector3 Rotation { get; init; }
    public Matrix4x4 Transform { get; init; } = Matrix4x4.Identity;
    public Matrix4x4 WorldMatrix { get; set; } = Matrix4x4.Identity;
    public List<RenderBone> Children { get; } = [];
}

/// <summary>
/// A node in the render graph.
/// </summary>
public sealed class RenderNode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Node";
    public RenderNodeType NodeType { get; init; } = RenderNodeType.Mesh;
    public Matrix4x4 LocalTransform { get; init; } = Matrix4x4.Identity;
    public Matrix4x4 WorldTransform { get; set; } = Matrix4x4.Identity;
    public List<RenderMesh> Meshes { get; } = [];
    public List<RenderVoxelChunk> VoxelChunks { get; } = [];
    public List<RenderBone> Bones { get; } = [];
    public List<RenderNode> Children { get; } = [];
    public Vector3 BoundsMin { get; set; }
    public Vector3 BoundsMax { get; set; }

    public bool HasGeometry => Meshes.Count > 0 || VoxelChunks.Count > 0 || Bones.Count > 0;
}

/// <summary>
/// Complete renderable scene snapshot prepared from USM.
/// </summary>
public sealed class RenderScene
{
    public string Name { get; init; } = "Untitled Scene";
    public List<RenderNode> RootNodes { get; } = [];
    public Dictionary<string, RenderMaterial> Materials { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<RenderBone> Skeletons { get; } = [];
    public Vector3 BoundsMin { get; set; }
    public Vector3 BoundsMax { get; set; }
    public int TotalTriangles { get; set; }
    public int TotalVertices { get; set; }
    public int TotalVoxels { get; set; }

    public Vector3 BoundsCenter => (BoundsMin + BoundsMax) * 0.5f;
    public Vector3 BoundsSize => BoundsMax - BoundsMin;
    public float BoundingSphereRadius => Math.Max(0.5f, Vector3.Distance(BoundsMin, BoundsMax) * 0.5f);
}

/// <summary>
/// Information about currently selected object or element in the viewport.
/// </summary>
public sealed record SelectionInfo(
    string? SelectedNodeId,
    string? SelectedMeshId,
    string? SelectedBoneName,
    Vector3? SelectedPosition,
    Vector3 BoundsMin,
    Vector3 BoundsMax,
    string SummaryText)
{
    public static SelectionInfo None => new(
        SelectedNodeId: null,
        SelectedMeshId: null,
        SelectedBoneName: null,
        SelectedPosition: null,
        BoundsMin: Vector3.Zero,
        BoundsMax: Vector3.Zero,
        SummaryText: "No selection");

    public bool HasSelection => !string.IsNullOrEmpty(SelectedNodeId) || !string.IsNullOrEmpty(SelectedBoneName) || SelectedPosition.HasValue;
}

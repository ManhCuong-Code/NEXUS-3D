using NEXUS3D.Domain.Enums;

namespace NEXUS3D.Conversion.Abstractions.Models;

/// <summary>
/// Fine-grained options configuring coordinate, geometry, material, and loss policies during conversion.
/// </summary>
public sealed class ConversionOptions
{
    public double ScaleFactor { get; set; } = 1.0;
    public bool InvertNormals { get; set; } = false;
    public bool AutoCorrectWindingOrder { get; set; } = true;
    public bool AdaptCoordinates { get; set; } = true;

    // Geometry Adaptation: Voxel -> Mesh
    public MeshingAlgorithm MeshingMode { get; set; } = MeshingAlgorithm.GreedyMeshing;
    public bool GenerateNormals { get; set; } = true;
    public bool GenerateUvs { get; set; } = true;

    // Geometry Adaptation: Mesh -> Voxel
    public VoxelizationMode VoxelizationMode { get; set; } = VoxelizationMode.SurfaceOnly;
    public VoxelResolutionMode VoxelResolutionMode { get; set; } = VoxelResolutionMode.AutomaticBounds;
    public double TargetVoxelSize { get; set; } = 1.0;
    public int TargetGridMaxResolution { get; set; } = 256;
    public bool SolidFillVoxels { get; set; } = false;

    // Bedrock Bones -> Static Mesh
    public bool BakeBedrockBonesToMesh { get; set; } = true;

    // Polygon Handling
    public bool TriangulatePolygons { get; set; } = false;

    // Material & Texture
    public bool PreserveMaterials { get; set; } = true;
    public bool CopyExternalTextures { get; set; } = true;
    public string? TextureOutputSubdirectory { get; set; } = "textures";

    // Data Loss & Governance
    public LossActionPolicy LossPolicy { get; set; } = LossActionPolicy.WarnAndProceed;

    // Custom Extension Parameters
    public Dictionary<string, string> CustomParameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static ConversionOptions Default => new();
}

/// <summary>
/// Options for transforming 3D coordinates between coordinate conventions.
/// </summary>
public sealed record CoordinateTransformOptions(
    bool FlipHandedness = true,
    bool InvertAxisX = false,
    bool InvertAxisY = false,
    bool InvertAxisZ = false,
    bool SwapYZ = false,
    double Scale = 1.0,
    bool InvertWindingOrder = true,
    bool InvertNormals = true);

/// <summary>
/// Options for converting voxel grids into polygon meshes.
/// </summary>
public sealed record VoxelToMeshOptions(
    MeshingAlgorithm Algorithm = MeshingAlgorithm.GreedyMeshing,
    bool GenerateNormals = true,
    bool GenerateUvs = true,
    bool SplitByPalette = false,
    double BlockSize = 1.0);

/// <summary>
/// Options for 3D voxelization of polygon meshes.
/// </summary>
public sealed record MeshToVoxelOptions(
    VoxelizationMode Mode = VoxelizationMode.SurfaceOnly,
    VoxelResolutionMode ResolutionMode = VoxelResolutionMode.AutomaticBounds,
    double VoxelSize = 1.0,
    int MaxGridDimension = 256,
    bool FillInterior = false,
    string DefaultBlockNamespace = "minecraft:stone");

/// <summary>
/// Options for baking Bedrock cuboid bones into static scene meshes.
/// </summary>
public sealed record BedrockCubeToMeshOptions(
    bool BakeTransforms = true,
    bool UnfoldUvs = true,
    bool IncludeLocators = true);

using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.VoxelJson.Models;

/// <summary>
/// Identifies the concrete Voxel JSON dialect used by a document.
/// </summary>
public enum VoxelJsonDialect
{
    /// <summary>Unknown or unrecognized JSON structure.</summary>
    Unknown = 0,

    /// <summary>
    /// NEXUS Canonical Voxel Grid JSON v1 (<c>"format": "nexus:voxel_grid_v1"</c> or structured <c>grid</c> + <c>voxels</c>).
    /// </summary>
    NexusVoxelGridV1 = 1,

    /// <summary>
    /// Sparse Voxel Array dialect (<c>"voxels"</c> array of coordinate objects or <c>[x,y,z,color]</c> tuples with optional <c>palette</c>/<c>dimension</c>).
    /// </summary>
    SparseVoxelArray = 2,

    /// <summary>
    /// Blockbench / Minecraft Java Block Model Cuboid dialect (<c>"elements"</c> array with <c>from</c>/<c>to</c> cuboids, <c>textures</c>, and <c>groups</c>).
    /// </summary>
    MinecraftBlockModelCuboids = 3
}

/// <summary>
/// Policy governing how duplicate voxel coordinates at the same position and layer are handled.
/// Duplicate voxels are never silently discarded.
/// </summary>
public enum DuplicateVoxelPolicy
{
    /// <summary>Keep all voxel entries in order and record a diagnostic warning when duplicates occur.</summary>
    KeepAll = 0,

    /// <summary>Keep the last voxel at a given coordinate/layer and record a diagnostic warning.</summary>
    KeepLastWithWarning = 1,

    /// <summary>Keep the first voxel at a given coordinate/layer and record a diagnostic warning.</summary>
    KeepFirstWithWarning = 2,

    /// <summary>Treat duplicate voxel coordinates as a validation/parse error (<see cref="JsonErrorCodes.InvalidVoxelElement"/>).</summary>
    RejectWithError = 3
}

/// <summary>
/// Options controlling Voxel JSON parsing and validation.
/// </summary>
public sealed record VoxelJsonReadOptions : JsonReadOptions
{
    public static new VoxelJsonReadOptions Default { get; } = new();

    /// <summary>Maximum number of voxel/cuboid elements permitted in a single document.</summary>
    public int MaxVoxelCount { get; init; } = 2_000_000;

    /// <summary>Policy for handling duplicate voxels at the same position and layer.</summary>
    public DuplicateVoxelPolicy DuplicatePolicy { get; init; } = DuplicateVoxelPolicy.KeepLastWithWarning;
}

/// <summary>
/// Root in-memory representation of a Voxel JSON document across all supported dialects.
/// </summary>
public sealed class VoxelDocument
{
    public VoxelJsonDialect Dialect { get; set; } = VoxelJsonDialect.NexusVoxelGridV1;
    public VoxelDocumentMetadata Metadata { get; set; } = new();
    public VoxelGridDefinition Grid { get; set; } = new();
    public List<VoxelMaterialReference> Materials { get; set; } = [];
    public List<VoxelLayerDefinition> Layers { get; set; } = [];
    public List<VoxelRecord> Voxels { get; set; } = [];
    public JsonExtensionData RootExtensionData { get; set; } = new();

    public VoxelDocument DeepClone() => new()
    {
        Dialect = Dialect,
        Metadata = Metadata.DeepClone(),
        Grid = Grid.DeepClone(),
        Materials = Materials.Select(m => m.DeepClone()).ToList(),
        Layers = Layers.Select(l => l.DeepClone()).ToList(),
        Voxels = Voxels.Select(v => v.DeepClone()).ToList(),
        RootExtensionData = RootExtensionData.DeepClone()
    };
}

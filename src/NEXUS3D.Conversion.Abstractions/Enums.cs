namespace NEXUS3D.Conversion.Abstractions;

/// <summary>
/// Specific 3D modeling and data features supported across various formats.
/// </summary>
[Flags]
public enum FormatFeature
{
    None = 0,
    VoxelGrid = 1 << 0,
    PolygonMesh = 1 << 1,
    SkeletalHierarchy = 1 << 2,
    BoneLocators = 1 << 3,
    PbrMaterials = 1 << 4,
    DiffuseTextures = 1 << 5,
    PerFaceUv = 1 << 6,
    BoxUv = 1 << 7,
    CustomNbtMetadata = 1 << 8,
    MultiMaterials = 1 << 9,
    AnimationSequences = 1 << 10,
    SmoothingGroups = 1 << 11,
    VertexColors = 1 << 12,
    CustomProperties = 1 << 13
}

/// <summary>
/// Severity levels for data loss, unmapped constructs, or approximations.
/// </summary>
public enum LossSeverity
{
    /// <summary>Feature is natively supported by both source and target with zero loss.</summary>
    Supported = 0,

    /// <summary>Feature is preserved in lossless raw extension data (e.g. unknown JSON / NBT tags).</summary>
    PreservedRaw = 1,

    /// <summary>Feature is converted using an algorithmic approximation (e.g., meshing voxel grids or triangulating n-gons).</summary>
    Approximated = 2,

    /// <summary>Feature is unmapped because the target format lacks a conceptual equivalent (e.g. bones in OBJ or NBT in standard OBJ).</summary>
    Unmapped = 3,

    /// <summary>Data is permanently discarded during export to the target format.</summary>
    Lost = 4
}

/// <summary>
/// Policy governing how conversions proceed when data loss is detected.
/// </summary>
public enum LossActionPolicy
{
    /// <summary>Proceed regardless of any loss or warnings.</summary>
    ProceedAlways = 0,

    /// <summary>Record warnings and proceed automatically.</summary>
    WarnAndProceed = 1,

    /// <summary>Require user confirmation before proceeding if high or critical loss is predicted.</summary>
    RequireConfirmationOnHighLoss = 2,

    /// <summary>Abort immediately if any loss is predicted.</summary>
    AbortOnAnyLoss = 3
}

/// <summary>
/// Stages of an executed conversion pipeline.
/// </summary>
public enum ConversionStepType
{
    SourceFormatDetection = 0,
    SourceParsingToUsm = 1,
    CoordinateAdaptation = 2,
    GeometryAdaptation = 3,
    MaterialMapping = 4,
    LossAnalysis = 5,
    TargetExport = 6
}

/// <summary>
/// Strategy for converting volumetric voxels to polygon surface meshes.
/// </summary>
public enum MeshingAlgorithm
{
    /// <summary>Generates 6 quads (12 triangles) for every voxel indiscriminately.</summary>
    SimpleBlockQuads = 0,

    /// <summary>Culls internal adjacent coplanar faces, generating quads only for exposed outer surfaces.</summary>
    ExposedFaceCulling = 1,

    /// <summary>Merges coplanar adjacent copalette quads into larger rectangles to minimize polygon count.</summary>
    GreedyMeshing = 2
}

/// <summary>
/// Strategy for converting continuous polygon meshes into discrete voxel grids.
/// </summary>
public enum VoxelizationMode
{
    /// <summary>Rasterizes triangles intersecting the 3D grid cell boundaries.</summary>
    SurfaceOnly = 0,

    /// <summary>Rasterizes surface boundary and floods interior cells to form solid volumes.</summary>
    SolidFill = 1,

    /// <summary>Conservative voxelization guaranteeing no triangle gap leaks.</summary>
    ConservativeSurface = 2
}

/// <summary>
/// Determines how the bounding grid dimensions are calculated during voxelization.
/// </summary>
public enum VoxelResolutionMode
{
    /// <summary>Calculates dimensions from bounding box divided by target voxel size.</summary>
    AutomaticBounds = 0,

    /// <summary>Specifies an exact metric/block voxel size.</summary>
    FixedVoxelSize = 1,

    /// <summary>Clamps the largest bounding dimension to a fixed grid resolution (e.g. 16, 32, 64, 128, 256).</summary>
    FixedGridDimension = 2
}

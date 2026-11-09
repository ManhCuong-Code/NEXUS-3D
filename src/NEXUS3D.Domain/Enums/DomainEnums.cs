namespace NEXUS3D.Domain.Enums;

/// <summary>
/// Supported 3D model format families in NEXUS 3D.
/// </summary>
public enum ModelFormatType
{
    Unknown = 0,
    Schematic = 1,
    Obj = 2,
    VoxelJson = 3,
    BedrockGeometry = 4
}

/// <summary>
/// Lifecycle status of a conversion job.
/// </summary>
public enum ConversionStatus
{
    Pending = 0,
    Queued = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

/// <summary>
/// Fundamental representation category of a 3D model.
/// </summary>
public enum GeometryKind
{
    Unknown = 0,
    VoxelGrid = 1,
    PolygonMesh = 2,
    CuboidHierarchy = 3,
    Hybrid = 4
}

/// <summary>
/// Up-axis convention for 3D coordinate systems.
/// </summary>
public enum CoordinateAxisUp
{
    YUp = 0,
    ZUp = 1
}


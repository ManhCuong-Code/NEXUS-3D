namespace NEXUS3D.Voxelization.Abstractions.Contracts;

/// <summary>
/// Mode of volumetric voxelization.
/// </summary>
public enum VoxelizationMode
{
    /// <summary>
    /// Rasterizes only the boundary shell where triangle faces intersect voxel cells.
    /// </summary>
    SurfaceOnly = 0,

    /// <summary>
    /// Rasterizes surface boundaries and fills the internal enclosed volume.
    /// </summary>
    SolidVolume = 1,

    /// <summary>
    /// Conservative rasterization with expanded bounding intersection to guarantee zero false-negatives on thin sheets.
    /// </summary>
    Conservative = 2,

    /// <summary>
    /// Adaptive multi-resolution voxelization preserving high detail at contours.
    /// </summary>
    Adaptive = 3
}

/// <summary>
/// Resolution calculation mode for voxel grid setup.
/// </summary>
public enum VoxelResolutionMode
{
    /// <summary>
    /// Explicit physical voxel cell size in world units (e.g. 1.0 = 1 meter per block).
    /// </summary>
    ByVoxelSize = 0,

    /// <summary>
    /// Scales voxel size automatically to fit the model within a maximum grid dimension along any axis (e.g. 64x64x64).
    /// </summary>
    ByMaxGridDimension = 1
}

/// <summary>
/// Strategy for deriving color and material on generated voxels.
/// </summary>
public enum ColorSamplingMode
{
    /// <summary>
    /// Selects material from the triangle with greatest surface area intersection inside the voxel.
    /// </summary>
    MaterialDominance = 0,

    /// <summary>
    /// Samples color from diffuse texture maps using barycentric UV interpolation.
    /// </summary>
    TextureSampling = 1,

    /// <summary>
    /// Interpolates vertex colors across triangle vertices.
    /// </summary>
    VertexColorInterpolation = 2,

    /// <summary>
    /// Applies fallback default palette block state uniformly.
    /// </summary>
    UniformFallback = 3
}

/// <summary>
/// Alignment of the discrete voxel grid relative to the input model bounds.
/// </summary>
public enum VoxelGridAlignment
{
    /// <summary>
    /// Centers the model coordinate center at the center of the voxel grid.
    /// </summary>
    CenterOnOrigin = 0,

    /// <summary>
    /// Aligns minimum bounds (bottom-left-front corner) with (0, 0, 0).
    /// </summary>
    MinAtOrigin = 1,

    /// <summary>
    /// Preserves exact world-space coordinates of the original mesh.
    /// </summary>
    PreserveWorldCoordinates = 2
}

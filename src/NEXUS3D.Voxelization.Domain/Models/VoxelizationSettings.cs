using NEXUS3D.Shared.Results;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;

namespace NEXUS3D.Voxelization.Domain.Models;

/// <summary>
/// Strong configuration object for voxelization engine.
/// </summary>
public sealed class VoxelizationSettings : IVoxelizationSettings
{
    public double VoxelSize { get; set; } = 1.0;
    public VoxelResolutionMode ResolutionMode { get; set; } = VoxelResolutionMode.ByMaxGridDimension;
    public int MaxGridDimension { get; set; } = 64;
    public long MaxVoxelCount { get; set; } = 5_000_000;

    public VoxelizationMode Mode { get; set; } = VoxelizationMode.SurfaceOnly;
    public ColorSamplingMode ColorSampling { get; set; } = ColorSamplingMode.MaterialDominance;
    public VoxelGridAlignment GridAlignment { get; set; } = VoxelGridAlignment.CenterOnOrigin;

    public bool PreserveMaterials { get; set; } = true;
    public bool PreserveTextures { get; set; } = true;
    public bool PreserveVertexColors { get; set; } = true;
    public bool PreserveOriginalTransform { get; set; } = true;

    public bool FillInternalGaps { get; set; } = true;
    public bool RemoveIsolatedVoxels { get; set; } = false;
    public bool MergeAdjacentVoxels { get; set; } = true;

    public string DefaultBlockNamespace { get; set; } = "minecraft:stone";

    public OperationResult<bool> Validate()
    {
        if (ResolutionMode == VoxelResolutionMode.ByVoxelSize && VoxelSize <= 0.0001)
        {
            return OperationResult<bool>.Failure(
                "VOX_INVALID_SIZE", "VoxelSize must be strictly positive and greater than 0.0001 world units.");
        }

        if (ResolutionMode == VoxelResolutionMode.ByMaxGridDimension && (MaxGridDimension < 4 || MaxGridDimension > 2048))
        {
            return OperationResult<bool>.Failure(
                "VOX_INVALID_RESOLUTION", "MaxGridDimension must be between 4 and 2048.");
        }

        if (MaxVoxelCount <= 0 || MaxVoxelCount > 100_000_000)
        {
            return OperationResult<bool>.Failure(
                "VOX_INVALID_MAX_VOXELS", "MaxVoxelCount limit must be between 1 and 100,000,000.");
        }

        if (string.IsNullOrWhiteSpace(DefaultBlockNamespace))
        {
            DefaultBlockNamespace = "minecraft:stone";
        }

        return OperationResult<bool>.Success(true);
    }
}

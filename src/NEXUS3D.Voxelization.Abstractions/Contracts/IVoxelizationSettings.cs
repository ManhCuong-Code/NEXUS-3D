using NEXUS3D.Shared.Results;

namespace NEXUS3D.Voxelization.Abstractions.Contracts;

/// <summary>
/// Settings and configuration for voxelization execution.
/// </summary>
public interface IVoxelizationSettings
{
    double VoxelSize { get; set; }
    VoxelResolutionMode ResolutionMode { get; set; }
    int MaxGridDimension { get; set; }
    long MaxVoxelCount { get; set; }

    VoxelizationMode Mode { get; set; }
    ColorSamplingMode ColorSampling { get; set; }
    VoxelGridAlignment GridAlignment { get; set; }

    bool PreserveMaterials { get; set; }
    bool PreserveTextures { get; set; }
    bool PreserveVertexColors { get; set; }
    bool PreserveOriginalTransform { get; set; }

    bool FillInternalGaps { get; set; }
    bool RemoveIsolatedVoxels { get; set; }
    bool MergeAdjacentVoxels { get; set; }

    string DefaultBlockNamespace { get; set; }

    OperationResult<bool> Validate();
}

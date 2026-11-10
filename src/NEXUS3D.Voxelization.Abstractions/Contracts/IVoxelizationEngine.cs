using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Voxelization.Abstractions.Models;

namespace NEXUS3D.Voxelization.Abstractions.Contracts;

/// <summary>
/// Core contract for the universal voxelization engine.
/// </summary>
public interface IVoxelizationEngine
{
    Task<VoxelizationResult> VoxelizeAsync(
        Scene3D scene,
        IVoxelizationSettings settings,
        IProgress<VoxelizationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<VoxelizationResult> VoxelizeMeshAsync(
        MeshData mesh,
        Scene3D? parentScene,
        IVoxelizationSettings settings,
        IProgress<VoxelizationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Individual algorithm strategy for voxelization (Surface, Solid, Conservative, Adaptive).
/// </summary>
public interface IVoxelizationAlgorithm
{
    string Name { get; }
    VoxelizationMode SupportedMode { get; }

    Task<VoxelizationResult> ExecuteAsync(
        MeshData mesh,
        Scene3D? scene,
        IVoxelizationSettings settings,
        IProgress<VoxelizationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Pre-execution validator for geometry and parameters.
/// </summary>
public interface IVoxelizationValidator
{
    (bool IsValid, string? ErrorMessage, IReadOnlyList<VoxelizationWarning> Warnings) Validate(
        Scene3D scene,
        IVoxelizationSettings settings);

    (bool IsValid, string? ErrorMessage, IReadOnlyList<VoxelizationWarning> Warnings) ValidateMesh(
        MeshData mesh,
        IVoxelizationSettings settings);
}

/// <summary>
/// Merges discrete voxels into optimal Axis-Aligned Bounding Box Cuboids.
/// </summary>
public interface IVoxelToCuboidConverter
{
    Task<CuboidOptimizationResult> ConvertToCuboidsAsync(
        NEXUS3D.Domain.Voxels.VoxelGrid grid,
        CancellationToken cancellationToken = default);
}

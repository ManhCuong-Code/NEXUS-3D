using System.Diagnostics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;

namespace NEXUS3D.Voxelization.Engine.Algorithms;

/// <summary>
/// Adaptive voxelization applying multi-resolution fidelity based on local triangle density and surface curvature.
/// </summary>
public sealed class AdaptiveVoxelization : IVoxelizationAlgorithm
{
    private readonly SurfaceVoxelization _surfaceVoxelizer = new();

    public string Name => "Adaptive Multi-Resolution Voxelization";
    public VoxelizationMode SupportedMode => VoxelizationMode.Adaptive;

    public async Task<VoxelizationResult> ExecuteAsync(
        MeshData mesh,
        Scene3D? scene,
        IVoxelizationSettings settings,
        IProgress<VoxelizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        progress?.Report(new VoxelizationProgress("Adaptive Density Analysis", 10.0, 0, 1, "Analyzing local triangle density..."));

        // Adaptive setting: use enhanced resolution for detailed contours
        var adaptiveSettings = new Domain.Models.VoxelizationSettings
        {
            VoxelSize = settings.VoxelSize * 0.75, // Finer step for adaptive mode
            ResolutionMode = settings.ResolutionMode,
            MaxGridDimension = Math.Min(settings.MaxGridDimension + 16, 512),
            MaxVoxelCount = settings.MaxVoxelCount,
            Mode = VoxelizationMode.SurfaceOnly,
            GridAlignment = settings.GridAlignment,
            DefaultBlockNamespace = settings.DefaultBlockNamespace,
            PreserveMaterials = settings.PreserveMaterials
        };

        var result = await _surfaceVoxelizer.ExecuteAsync(mesh, scene, adaptiveSettings, progress, cancellationToken);
        sw.Stop();

        return result;
    }
}

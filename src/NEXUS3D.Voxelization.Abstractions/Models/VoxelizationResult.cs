using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Voxelization.Abstractions.Models;

/// <summary>
/// Overall result of a voxelization run including resulting grid, cuboids, warnings, and performance stats.
/// </summary>
public sealed class VoxelizationResult
{
    public bool IsSuccess { get; init; }
    public VoxelGrid? VoxelGrid { get; init; }
    public IReadOnlyList<VoxelCuboid>? Cuboids { get; init; }
    public VoxelizationStatistics Statistics { get; init; } = VoxelizationStatistics.Empty;
    public IReadOnlyList<VoxelizationWarning> Warnings { get; init; } = [];
    public string? ErrorMessage { get; init; }

    public static VoxelizationResult Success(
        VoxelGrid grid,
        VoxelizationStatistics stats,
        IReadOnlyList<VoxelCuboid>? cuboids = null,
        IReadOnlyList<VoxelizationWarning>? warnings = null) =>
        new()
        {
            IsSuccess = true,
            VoxelGrid = grid,
            Cuboids = cuboids,
            Statistics = stats,
            Warnings = warnings ?? []
        };

    public static VoxelizationResult Failure(
        string errorMessage,
        IReadOnlyList<VoxelizationWarning>? warnings = null) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Warnings = warnings ?? []
        };
}

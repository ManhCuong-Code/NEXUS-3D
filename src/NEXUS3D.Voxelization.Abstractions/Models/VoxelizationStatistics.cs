namespace NEXUS3D.Voxelization.Abstractions.Models;

/// <summary>
/// Detailed execution statistics for a voxelization operation.
/// </summary>
public sealed record VoxelizationStatistics
{
    public int TotalTrianglesProcessed { get; init; }
    public int InitialVoxelCount { get; init; }
    public int FinalVoxelCount { get; init; }
    public int OptimizedCuboidCount { get; init; }
    public int IsolatedVoxelsPruned { get; init; }
    public int InternalVoxelsFilled { get; init; }

    public int GridDimensionX { get; init; }
    public int GridDimensionY { get; init; }
    public int GridDimensionZ { get; init; }

    public double ActualVoxelSize { get; init; }
    public long ProcessingTimeMs { get; init; }
    public long EstimatedMemoryBytes { get; init; }

    public double ElementReductionRatio =>
        FinalVoxelCount > 0 && OptimizedCuboidCount > 0
            ? (1.0 - ((double)OptimizedCuboidCount / FinalVoxelCount)) * 100.0
            : 0.0;

    public static VoxelizationStatistics Empty => new();
}

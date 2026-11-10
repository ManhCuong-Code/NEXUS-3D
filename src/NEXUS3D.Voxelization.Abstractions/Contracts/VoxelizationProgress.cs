namespace NEXUS3D.Voxelization.Abstractions.Contracts;

/// <summary>
/// Real-time progress notification for long-running voxelization tasks.
/// </summary>
public sealed record VoxelizationProgress(
    string Stage,
    double ProgressPercentage,
    int CurrentItem,
    int TotalItems,
    string Message)
{
    public static VoxelizationProgress Initial(string message = "Preparing voxelization...") =>
        new("Initialization", 0.0, 0, 0, message);

    public static VoxelizationProgress Completed(string message = "Voxelization completed.") =>
        new("Complete", 100.0, 1, 1, message);
}

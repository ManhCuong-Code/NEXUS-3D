using NEXUS3D.Domain.Voxels;
using NEXUS3D.Voxelization.Engine.Processing;
using Xunit;

namespace NEXUS3D.Voxelization.Tests;

public class VoxelOptimizationServiceTests
{
    private readonly VoxelOptimizationService _service = new();

    [Fact]
    public void PruneIsolatedVoxels_SingleIsolatedVoxel_IsRemoved()
    {
        var grid = new VoxelGrid("IsolatedTest");
        var state = new BlockState("minecraft", "stone");
        int pal = grid.Palette.GetOrAdd(state);

        // Add 1 solitary voxel at (10, 10, 10) with 0 neighbors
        grid.SetVoxel(new Voxel(new VoxelPosition(10, 10, 10), pal, state));

        // Add a cluster of 4 connected voxels at origin
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), pal, state));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 0), pal, state));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 1, 0), pal, state));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 1, 0), pal, state));

        int pruned = _service.PruneIsolatedVoxels(grid);

        Assert.Equal(1, pruned);
        Assert.False(grid.TryGetVoxel(new VoxelPosition(10, 10, 10), out _));
        Assert.True(grid.TryGetVoxel(new VoxelPosition(0, 0, 0), out _));
    }

    [Fact]
    public void FillInternalGaps_PinholeCavitySurroundedBy6Neighbors_IsFilled()
    {
        var grid = new VoxelGrid("GapTest");
        var state = new BlockState("minecraft", "stone");
        int pal = grid.Palette.GetOrAdd(state);

        // Surround position (1, 1, 1) on all 6 faces (+X, -X, +Y, -Y, +Z, -Z)
        grid.SetVoxel(new Voxel(new VoxelPosition(2, 1, 1), pal, state)); // +X
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 1, 1), pal, state)); // -X
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 2, 1), pal, state)); // +Y
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 1), pal, state)); // -Y
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 1, 2), pal, state)); // +Z
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 1, 0), pal, state)); // -Z

        // Verify cavity is empty before fill
        Assert.False(grid.TryGetVoxel(new VoxelPosition(1, 1, 1), out _));

        int filled = _service.FillInternalGaps(grid);

        Assert.True(filled >= 1);
        Assert.True(grid.TryGetVoxel(new VoxelPosition(1, 1, 1), out var filledVoxel));
        Assert.NotNull(filledVoxel);
        Assert.Equal(pal, filledVoxel.PaletteIndex);
    }
}

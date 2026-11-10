using NEXUS3D.Domain.Voxels;
using NEXUS3D.Voxelization.Engine.Conversion;
using Xunit;

namespace NEXUS3D.Voxelization.Tests;

public class VoxelToCuboidConverterTests
{
    private readonly VoxelToCuboidConverter _converter = new();

    [Fact]
    public async Task ConvertToCuboidsAsync_Solid4x4x4Block_MergesIntoSingleCuboid()
    {
        var grid = new VoxelGrid("SolidBlock");
        var state = new BlockState("minecraft", "stone");
        int palIdx = grid.Palette.GetOrAdd(state);

        // Fill 4x4x4 block [0..3, 0..3, 0..3]
        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                for (int z = 0; z < 4; z++)
                {
                    grid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), palIdx, state));
                }
            }
        }

        var result = await _converter.ConvertToCuboidsAsync(grid);

        Assert.Equal(64, result.OriginalVoxelCount);
        Assert.Single(result.Cuboids);
        Assert.Equal(64, result.Cuboids[0].Volume);
        Assert.Equal(new VoxelPosition(0, 0, 0), result.Cuboids[0].MinPosition);
        Assert.Equal(new VoxelPosition(3, 3, 3), result.Cuboids[0].MaxPosition);
        Assert.True(result.ReductionPercentage > 95.0);
    }

    [Fact]
    public async Task ConvertToCuboidsAsync_TwoMaterialsSideBySide_PreservesMaterialBoundaries()
    {
        var grid = new VoxelGrid("MultiMaterial");
        var stone = new BlockState("minecraft", "stone");
        var dirt = new BlockState("minecraft", "dirt");
        int stonePal = grid.Palette.GetOrAdd(stone);
        int dirtPal = grid.Palette.GetOrAdd(dirt);

        // Stone block [0..1, 0..1, 0..1] (8 voxels)
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
            grid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), stonePal, stone));

        // Dirt block [2..3, 0..1, 0..1] (8 voxels) directly adjacent along X
        for (int x = 2; x < 4; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
            grid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), dirtPal, dirt));

        var result = await _converter.ConvertToCuboidsAsync(grid);

        Assert.Equal(16, result.OriginalVoxelCount);
        // Expect exactly 2 cuboids (one for stone, one for dirt)
        Assert.Equal(2, result.Cuboids.Count);
        Assert.Contains(result.Cuboids, c => c.PaletteIndex == stonePal && c.Volume == 8);
        Assert.Contains(result.Cuboids, c => c.PaletteIndex == dirtPal && c.Volume == 8);

        // Total volume strictly equals 16
        int totalVolume = result.Cuboids.Sum(c => c.Volume);
        Assert.Equal(16, totalVolume);
    }

    [Fact]
    public async Task ConvertToCuboidsAsync_EmptyGrid_ReturnsZeroCuboids()
    {
        var grid = new VoxelGrid("Empty");
        var result = await _converter.ConvertToCuboidsAsync(grid);

        Assert.Equal(0, result.OriginalVoxelCount);
        Assert.Empty(result.Cuboids);
        Assert.Equal(0.0, result.ReductionPercentage);
    }
}

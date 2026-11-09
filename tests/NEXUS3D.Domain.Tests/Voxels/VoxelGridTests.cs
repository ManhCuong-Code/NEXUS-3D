using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Domain.Tests.Voxels;

public sealed class VoxelGridTests
{
    [Fact]
    public void VoxelPalette_ShouldDeduplicateCanonicalBlockStates_AndPreserveDistinctProperties()
    {
        var palette = new VoxelPalette();

        var stairsNorthA = new BlockState("minecraft", "oak_stairs", new Dictionary<string, string>
        {
            ["facing"] = "north",
            ["half"] = "bottom"
        });

        var stairsNorthB = new BlockState("minecraft", "oak_stairs", new Dictionary<string, string>
        {
            ["half"] = "bottom",
            ["facing"] = "north"
        });

        var stairsSouth = new BlockState("minecraft", "oak_stairs", new Dictionary<string, string>
        {
            ["facing"] = "south",
            ["half"] = "bottom"
        });

        int idx1 = palette.GetOrAdd(stairsNorthA);
        int idx2 = palette.GetOrAdd(stairsNorthB);
        int idx3 = palette.GetOrAdd(stairsSouth);

        Assert.Equal(idx1, idx2);
        Assert.NotEqual(idx1, idx3);
        Assert.Equal(2, palette.Count);
    }

    [Fact]
    public void VoxelGrid_SetGetRemoveBoundsAndValidation_ShouldWorkCorrectly()
    {
        var grid = new VoxelGrid("TestSchemGrid");
        int stoneIdx = grid.Palette.GetOrAdd(new BlockState("minecraft", "stone"));
        int chestIdx = grid.Palette.GetOrAdd(new BlockState("minecraft", "chest"));

        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), stoneIdx));
        grid.SetVoxel(new Voxel(new VoxelPosition(3, 4, 5), chestIdx, blockEntity: new BlockEntityData("minecraft:chest")));

        Assert.Equal(2, grid.GetVoxelCount());
        Assert.True(grid.Bounds.IsValid);
        Assert.Equal(new VoxelPosition(0, 0, 0), grid.Bounds.Min);
        Assert.Equal(new VoxelPosition(3, 4, 5), grid.Bounds.Max);
        Assert.Equal(4, grid.Bounds.Width);
        Assert.Equal(5, grid.Bounds.Height);
        Assert.Equal(6, grid.Bounds.Depth);

        BoundingBox worldBounds = grid.GetWorldBounds();
        Assert.Equal(new Vector3D(0, 0, 0), worldBounds.Min);
        Assert.Equal(new Vector3D(4, 5, 6), worldBounds.Max);

        var validResult = grid.Validate();
        Assert.True(validResult.IsValid);

        // Add voxel with out-of-range palette index -> fails with USM008
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 1, 1), paletteIndex: 999));
        var invalidResult = grid.Validate();
        Assert.False(invalidResult.IsValid);
        Assert.Contains(invalidResult.Issues, i => i.Code == UsmErrorCodes.InvalidPaletteIndex);
    }
}

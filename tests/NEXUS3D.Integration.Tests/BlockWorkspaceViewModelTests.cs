using NEXUS3D.App.ViewModels;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Integration.Tests;

public sealed class BlockWorkspaceViewModelTests
{
    private static Scene3D CreateSampleSchematicScene()
    {
        var scene = new Scene3D { Name = "SampleSchematic" };
        var grid = new VoxelGrid("MainGrid");

        var stairsProps = new Dictionary<string, string>
        {
            ["facing"] = "north",
            ["half"] = "bottom"
        };
        var stairsState = new BlockState("minecraft", "oak_stairs", stairsProps);
        var stoneState = new BlockState("minecraft", "stone");
        var glassState = new BlockState("minecraft", "glass");

        int palStone = grid.Palette.GetOrAdd(stoneState);
        int palStairs = grid.Palette.GetOrAdd(stairsState);
        int palGlass = grid.Palette.GetOrAdd(glassState);

        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), palStone, stoneState));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 0), palStone, stoneState));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 1, 0), palStairs, stairsState));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 2, 0), palGlass, glassState));

        var node = new SceneNode("VoxelRoot", new SceneObject("VoxelObject") { VoxelGrids = { grid } });
        scene.Graph.AddNode(scene.Graph.Root, node);
        return scene;
    }

    [Fact]
    public void LoadFromScene_PopulatesPaletteAndIndividualBlocks()
    {
        var vm = new BlockWorkspaceViewModel();
        var scene = CreateSampleSchematicScene();

        vm.LoadFromScene(scene);

        Assert.Equal(4, vm.TotalBlocks);
        Assert.Equal(3, vm.UniqueBlockTypes);
        Assert.Equal(3, vm.PaletteItems.Count);
        Assert.Equal(4, vm.AllIndividualBlocks.Count);
        Assert.Equal(4, vm.FilteredIndividualBlocks.Count);
    }

    [Fact]
    public void SelectIndividualBlock_UpdatesInspectorProperties()
    {
        var vm = new BlockWorkspaceViewModel();
        var scene = CreateSampleSchematicScene();
        vm.LoadFromScene(scene);

        var stairsBlock = vm.AllIndividualBlocks.First(b => b.BlockId == "minecraft:oak_stairs");
        vm.SelectIndividualBlock(stairsBlock);

        Assert.Equal(stairsBlock, vm.SelectedIndividualBlock);
        Assert.Equal(new VoxelPosition(0, 1, 0), vm.SelectedVoxelCoord);
        Assert.Equal("X: 0, Y: 1, Z: 0", vm.SelectedBlockPosition);
        Assert.Equal("minecraft:oak_stairs", vm.SelectedBlockId);
        Assert.Equal("minecraft:oak_stairs[facing=north,half=bottom]", vm.SelectedBlockState);
        Assert.NotNull(vm.SelectedBlockMaterial);
        Assert.Equal(stairsBlock.PaletteIndex, vm.SelectedBlockPaletteIndex);
        Assert.Equal("minecraft:oak_stairs", vm.SelectedPaletteItem?.BlockId);
    }

    [Fact]
    public void SelectVoxel_ByCoordinate_FindsAndSelectsBlock()
    {
        var vm = new BlockWorkspaceViewModel();
        var scene = CreateSampleSchematicScene();
        vm.LoadFromScene(scene);

        vm.SelectVoxel(new VoxelPosition(0, 2, 0), "minecraft:glass", "minecraft:glass");

        Assert.NotNull(vm.SelectedIndividualBlock);
        Assert.Equal(new VoxelPosition(0, 2, 0), vm.SelectedVoxelCoord);
        Assert.Equal("minecraft:glass", vm.SelectedBlockId);
        Assert.Equal("minecraft:glass", vm.SelectedBlockState);
    }

    [Fact]
    public void EmitsSelectionAndHighlightEvents_ToViewport()
    {
        var vm = new BlockWorkspaceViewModel();
        var scene = CreateSampleSchematicScene();
        vm.LoadFromScene(scene);

        string? highlightedBlock = null;
        VoxelPosition? selectedCoord = null;
        IndividualBlockItemViewModel? selectedItem = null;

        vm.BlockHighlightRequested += (s, id) => highlightedBlock = id;
        vm.BlockCoordinateSelected += (s, coord) => selectedCoord = coord;
        vm.IndividualBlockSelected += (s, item) => selectedItem = item;

        var targetBlock = vm.AllIndividualBlocks.First(b => b.Position == new VoxelPosition(1, 0, 0));
        vm.SelectIndividualBlock(targetBlock);

        Assert.Equal("minecraft:stone", highlightedBlock);
        Assert.Equal(new VoxelPosition(1, 0, 0), selectedCoord);
        Assert.Equal(targetBlock, selectedItem);
    }

    [Fact]
    public void SearchFilter_FiltersByBlockIdAndCoordinates()
    {
        var vm = new BlockWorkspaceViewModel();
        var scene = CreateSampleSchematicScene();
        vm.LoadFromScene(scene);

        // Filter by block id
        vm.SearchFilter = "oak_stairs";
        Assert.Single(vm.FilteredIndividualBlocks);
        Assert.Equal("minecraft:oak_stairs", vm.FilteredIndividualBlocks[0].BlockId);
        Assert.Single(vm.FilteredPaletteItems);

        // Filter by coordinates
        vm.SearchFilter = "0, 2, 0";
        Assert.Single(vm.FilteredIndividualBlocks);
        Assert.Equal(new VoxelPosition(0, 2, 0), vm.FilteredIndividualBlocks[0].Position);

        // Clear filter
        vm.SearchFilter = string.Empty;
        Assert.Equal(4, vm.FilteredIndividualBlocks.Count);
        Assert.Equal(3, vm.FilteredPaletteItems.Count);
    }

    [Fact]
    public void GroupByType_FilterWorksCorrectly()
    {
        var vm = new BlockWorkspaceViewModel();
        var scene = CreateSampleSchematicScene();
        vm.LoadFromScene(scene);

        // Filter for Transparent / Cutout
        vm.SelectedGroupType = "Transparent";
        Assert.All(vm.FilteredIndividualBlocks, b => Assert.Equal(BlockTransparencyKind.Transparent, b.Transparency));

        vm.SelectedGroupType = "All";
        Assert.Equal(4, vm.FilteredIndividualBlocks.Count);
    }

    [Fact]
    public void VisibilityCommands_ShowAllAndHideOther()
    {
        var vm = new BlockWorkspaceViewModel();
        var scene = CreateSampleSchematicScene();
        vm.LoadFromScene(scene);

        var stonePalette = vm.PaletteItems.First(p => p.BlockId == "minecraft:stone");
        vm.HideOtherBlocks(stonePalette);

        Assert.True(stonePalette.IsVisible);
        Assert.All(vm.PaletteItems.Where(p => p.BlockId != "minecraft:stone"), p => Assert.False(p.IsVisible));

        vm.ShowAllBlocks();
        Assert.All(vm.PaletteItems, p => Assert.True(p.IsVisible));
    }
}

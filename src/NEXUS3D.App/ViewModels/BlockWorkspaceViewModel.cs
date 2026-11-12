using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Rendering.Services;

namespace NEXUS3D.App.ViewModels;

public sealed partial class BlockPaletteItemViewModel : ObservableObject
{
    public required string BlockId { get; init; }
    public required string DisplayName { get; init; }
    public required int Count { get; init; }
    public required string ColorHex { get; init; }
    public BlockTransparencyKind Transparency { get; init; } = BlockTransparencyKind.Opaque;
    public string TransparencyBadge => Transparency.ToString();

    [ObservableProperty]
    private bool _isVisible = true;
}

/// <summary>
/// Represents an individual voxel block instance within a schematic for exploration and inspector display.
/// </summary>
public sealed partial class IndividualBlockItemViewModel : ObservableObject
{
    public required VoxelPosition Position { get; init; }
    public required string BlockId { get; init; }
    public required string DisplayName { get; init; }
    public required string StateString { get; init; }
    public required int PaletteIndex { get; init; }
    public required string ColorHex { get; init; }
    public required string MaterialName { get; init; }
    public BlockTransparencyKind Transparency { get; init; } = BlockTransparencyKind.Opaque;
    public string TransparencyBadge => Transparency.ToString();
    public string GroupType => Transparency.ToString();
    public string CoordinatesDisplay => $"({Position.X}, {Position.Y}, {Position.Z})";

    public int X => Position.X;
    public int Y => Position.Y;
    public int Z => Position.Z;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Specialized ViewModel managing Minecraft Schematic Block Explorer, Palette recognition,
/// individual block picking, and Inspector property presentation.
/// </summary>
public sealed partial class BlockWorkspaceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private string _selectedGroupType = "All";

    [ObservableProperty]
    private bool _groupByType;

    [ObservableProperty]
    private int _selectedViewModeIndex; // 0 = Palette Explorer, 1 = Individual Blocks Explorer

    [ObservableProperty]
    private int _totalBlocks;

    [ObservableProperty]
    private int _uniqueBlockTypes;

    [ObservableProperty]
    private BlockPaletteItemViewModel? _selectedPaletteItem;

    [ObservableProperty]
    private IndividualBlockItemViewModel? _selectedIndividualBlock;

    [ObservableProperty]
    private string _selectedBlockInfo = "No block selected";

    [ObservableProperty]
    private string _selectedBlockPosition = "X: 0, Y: 0, Z: 0";

    [ObservableProperty]
    private VoxelPosition? _selectedVoxelCoord;

    [ObservableProperty]
    private string _selectedBlockId = "minecraft:stone";

    [ObservableProperty]
    private string _selectedBlockState = "minecraft:stone";

    [ObservableProperty]
    private string _selectedBlockMaterial = "Stone";

    [ObservableProperty]
    private int _selectedBlockPaletteIndex;

    [ObservableProperty]
    private string _selectedBlockColorHex = "#808080";

    [ObservableProperty]
    private string _selectedBlockTransparency = "Opaque";

    [ObservableProperty]
    private bool _showChunkBoundaries;

    [ObservableProperty]
    private bool _showCoordinates = true;

    public ObservableCollection<string> AvailableGroupTypes { get; } =
        ["All", "Opaque", "Transparent", "Translucent", "Cutout"];

    public ObservableCollection<BlockPaletteItemViewModel> PaletteItems { get; } = [];
    public ObservableCollection<BlockPaletteItemViewModel> FilteredPaletteItems { get; } = [];

    public List<IndividualBlockItemViewModel> AllIndividualBlocks { get; } = [];
    public ObservableCollection<IndividualBlockItemViewModel> FilteredIndividualBlocks { get; } = [];

    public event EventHandler<string>? BlockFilterChanged;
    public event EventHandler<string?>? BlockHighlightRequested;
    public event EventHandler<IndividualBlockItemViewModel?>? IndividualBlockSelected;
    public event EventHandler<VoxelPosition?>? BlockCoordinateSelected;

    public void LoadFromScene(Scene3D scene)
    {
        PaletteItems.Clear();
        FilteredPaletteItems.Clear();
        AllIndividualBlocks.Clear();
        FilteredIndividualBlocks.Clear();
        SelectedPaletteItem = null;
        SelectedIndividualBlock = null;

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var blocksList = new List<IndividualBlockItemViewModel>();

        foreach (var node in scene.Graph.Nodes)
        {
            if (node.Object is not { } obj) continue;
            foreach (var grid in obj.VoxelGrids)
            {
                foreach (var voxel in grid.Voxels)
                {
                    string id = voxel.State?.GetFullIdentifier() ??
                        (grid.Palette.TryGet(voxel.PaletteIndex, out var pe) ? pe?.State.GetFullIdentifier() : null) ??
                        "minecraft:stone";

                    string stateStr = voxel.State?.GetCanonicalStateKey() ??
                        (grid.Palette.TryGet(voxel.PaletteIndex, out var pe2) ? pe2?.State.GetCanonicalStateKey() : null) ??
                        id;

                    counts[id] = counts.GetValueOrDefault(id) + 1;

                    var info = BlockMaterialRegistry.GetBlockInfo(id);
                    int r = (int)(info.Color.X * 255);
                    int g = (int)(info.Color.Y * 255);
                    int b = (int)(info.Color.Z * 255);
                    string hex = $"#{r:X2}{g:X2}{b:X2}";

                    var individual = new IndividualBlockItemViewModel
                    {
                        Position = voxel.Position,
                        BlockId = id,
                        DisplayName = info.DisplayName,
                        StateString = stateStr,
                        PaletteIndex = voxel.PaletteIndex,
                        ColorHex = hex,
                        MaterialName = $"{info.DisplayName} (Roughness: {info.Roughness:F2}, {info.Transparency})",
                        Transparency = info.Transparency,
                        IsVisible = true
                    };
                    blocksList.Add(individual);
                }
            }
        }

        AllIndividualBlocks.AddRange(blocksList);

        int total = 0;
        foreach (var (id, count) in counts.OrderByDescending(kv => kv.Value))
        {
            total += count;
            var info = BlockMaterialRegistry.GetBlockInfo(id);
            int r = (int)(info.Color.X * 255);
            int g = (int)(info.Color.Y * 255);
            int b = (int)(info.Color.Z * 255);
            string hex = $"#{r:X2}{g:X2}{b:X2}";

            var item = new BlockPaletteItemViewModel
            {
                BlockId = id,
                DisplayName = info.DisplayName,
                Count = count,
                ColorHex = hex,
                Transparency = info.Transparency,
                IsVisible = true
            };
            PaletteItems.Add(item);
        }

        TotalBlocks = total;
        UniqueBlockTypes = PaletteItems.Count;
        ApplyFilter();

        if (AllIndividualBlocks.Count > 0)
        {
            SelectIndividualBlock(AllIndividualBlocks[0]);
        }
        else if (PaletteItems.Count > 0)
        {
            SelectBlock(PaletteItems[0]);
        }
    }

    partial void OnSearchFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedGroupTypeChanged(string value) => ApplyFilter();
    partial void OnGroupByTypeChanged(bool value) => ApplyFilter();

    public void ApplyFilter()
    {
        // 1. Filter Palette Items
        FilteredPaletteItems.Clear();
        var q = SearchFilter?.Trim() ?? string.Empty;

        foreach (var item in PaletteItems)
        {
            if (MatchesFilter(item.DisplayName, item.BlockId, item.Transparency.ToString(), q))
            {
                FilteredPaletteItems.Add(item);
            }
        }

        // 2. Filter Individual Blocks (virtualized collection with performance cap)
        FilteredIndividualBlocks.Clear();
        IEnumerable<IndividualBlockItemViewModel> blockQuery = AllIndividualBlocks;

        // Group / Transparency filter
        if (!string.Equals(SelectedGroupType, "All", StringComparison.OrdinalIgnoreCase))
        {
            blockQuery = blockQuery.Where(b => string.Equals(b.Transparency.ToString(), SelectedGroupType, StringComparison.OrdinalIgnoreCase));
        }

        // Text search / Block ID filter
        if (!string.IsNullOrEmpty(q))
        {
            blockQuery = blockQuery.Where(b =>
                b.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.BlockId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.StateString.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.CoordinatesDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.X.ToString().Equals(q, StringComparison.OrdinalIgnoreCase) ||
                b.Y.ToString().Equals(q, StringComparison.OrdinalIgnoreCase) ||
                b.Z.ToString().Equals(q, StringComparison.OrdinalIgnoreCase));
        }

        if (GroupByType)
        {
            blockQuery = blockQuery.OrderBy(b => b.Transparency.ToString()).ThenBy(b => b.DisplayName);
        }

        const int maxDisplayItems = 10000;
        int count = 0;
        foreach (var block in blockQuery)
        {
            FilteredIndividualBlocks.Add(block);
            if (++count >= maxDisplayItems) break;
        }
    }

    private bool MatchesFilter(string displayName, string blockId, string transparency, string query)
    {
        bool groupMatch = string.Equals(SelectedGroupType, "All", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(transparency, SelectedGroupType, StringComparison.OrdinalIgnoreCase);

        if (!groupMatch) return false;

        if (string.IsNullOrEmpty(query)) return true;

        return displayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               blockId.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    public void SelectBlock(BlockPaletteItemViewModel? item)
    {
        SelectedPaletteItem = item;
        if (item is null)
        {
            SelectedBlockInfo = "No block selected";
            SelectedVoxelCoord = null;
            BlockHighlightRequested?.Invoke(this, null);
            BlockCoordinateSelected?.Invoke(this, null);
            return;
        }

        SelectedBlockInfo = $"{item.DisplayName} ({item.Count:N0} instances)";
        SelectedBlockId = item.BlockId;
        SelectedBlockState = item.BlockId;
        SelectedBlockColorHex = item.ColorHex;
        SelectedBlockTransparency = item.TransparencyBadge;
        SelectedBlockPosition = "Multiple Instances";
        SelectedVoxelCoord = null;
        SelectedBlockMaterial = $"{item.DisplayName} ({item.TransparencyBadge})";

        // Find first matching individual block if available
        var matchingBlock = AllIndividualBlocks.FirstOrDefault(b => string.Equals(b.BlockId, item.BlockId, StringComparison.OrdinalIgnoreCase));
        if (matchingBlock is not null)
        {
            SelectedBlockPaletteIndex = matchingBlock.PaletteIndex;
        }

        BlockHighlightRequested?.Invoke(this, item.BlockId);
        BlockCoordinateSelected?.Invoke(this, null);
    }

    [RelayCommand]
    public void SelectIndividualBlock(IndividualBlockItemViewModel? block)
    {
        if (SelectedIndividualBlock is not null)
        {
            SelectedIndividualBlock.IsSelected = false;
        }

        SelectedIndividualBlock = block;
        if (block is null)
        {
            SelectedBlockInfo = "No block selected";
            SelectedVoxelCoord = null;
            IndividualBlockSelected?.Invoke(this, null);
            BlockCoordinateSelected?.Invoke(this, null);
            BlockHighlightRequested?.Invoke(this, null);
            return;
        }

        block.IsSelected = true;
        SelectedVoxelCoord = block.Position;
        SelectedBlockPosition = $"X: {block.Position.X}, Y: {block.Position.Y}, Z: {block.Position.Z}";
        SelectedBlockId = block.BlockId;
        SelectedBlockState = block.StateString;
        SelectedBlockMaterial = block.MaterialName;
        SelectedBlockPaletteIndex = block.PaletteIndex;
        SelectedBlockColorHex = block.ColorHex;
        SelectedBlockTransparency = block.TransparencyBadge;
        SelectedBlockInfo = $"{block.DisplayName} at ({block.Position.X}, {block.Position.Y}, {block.Position.Z})";

        // Keep palette selection synchronized
        SelectedPaletteItem = PaletteItems.FirstOrDefault(p => string.Equals(p.BlockId, block.BlockId, StringComparison.OrdinalIgnoreCase));

        IndividualBlockSelected?.Invoke(this, block);
        BlockCoordinateSelected?.Invoke(this, block.Position);
        BlockHighlightRequested?.Invoke(this, block.BlockId);
    }

    public void SelectVoxel(VoxelPosition position, string? blockId = null, string? blockState = null)
    {
        var found = AllIndividualBlocks.FirstOrDefault(b => b.Position == position);
        if (found is not null)
        {
            SelectIndividualBlock(found);
            return;
        }

        var id = blockId ?? "minecraft:stone";
        var info = BlockMaterialRegistry.GetBlockInfo(id);
        int r = (int)(info.Color.X * 255);
        int g = (int)(info.Color.Y * 255);
        int b = (int)(info.Color.Z * 255);
        string hex = $"#{r:X2}{g:X2}{b:X2}";

        var dynamicBlock = new IndividualBlockItemViewModel
        {
            Position = position,
            BlockId = id,
            DisplayName = info.DisplayName,
            StateString = blockState ?? id,
            PaletteIndex = 0,
            ColorHex = hex,
            MaterialName = $"{info.DisplayName} (Roughness: {info.Roughness:F2}, {info.Transparency})",
            Transparency = info.Transparency
        };

        SelectIndividualBlock(dynamicBlock);
    }

    [RelayCommand]
    public void FilterByBlock(string? blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId)) return;
        SearchFilter = blockId;
    }

    [RelayCommand]
    public void ToggleBlockVisibility(BlockPaletteItemViewModel? item)
    {
        if (item is null) return;
        item.IsVisible = !item.IsVisible;
        BlockFilterChanged?.Invoke(this, item.BlockId);
    }

    [RelayCommand]
    public void ShowAllBlocks()
    {
        foreach (var item in PaletteItems)
        {
            item.IsVisible = true;
        }
        BlockFilterChanged?.Invoke(this, "*");
    }

    [RelayCommand]
    public void HideUnselectedBlocks() => HideOtherBlocks(SelectedPaletteItem);

    [RelayCommand]
    public void HideOtherBlocks(BlockPaletteItemViewModel? item)
    {
        if (item is null) return;
        foreach (var p in PaletteItems)
        {
            p.IsVisible = ReferenceEquals(p, item);
        }
        BlockFilterChanged?.Invoke(this, item.BlockId);
    }

    [RelayCommand]
    public void ToggleChunkBoundaries()
    {
        ShowChunkBoundaries = !ShowChunkBoundaries;
    }

    [RelayCommand]
    public void ToggleCoordinates()
    {
        ShowCoordinates = !ShowCoordinates;
    }
}

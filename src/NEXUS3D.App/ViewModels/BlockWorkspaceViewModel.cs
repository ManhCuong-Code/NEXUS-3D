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

    [ObservableProperty]
    private bool _isVisible = true;
}

/// <summary>
/// Specialized ViewModel managing Minecraft Schematic Block Explorer, Palette recognition, and Block inspection.
/// </summary>
public sealed partial class BlockWorkspaceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private int _totalBlocks;

    [ObservableProperty]
    private int _uniqueBlockTypes;

    [ObservableProperty]
    private BlockPaletteItemViewModel? _selectedPaletteItem;

    [ObservableProperty]
    private string _selectedBlockInfo = "No block selected";

    [ObservableProperty]
    private string _selectedBlockPosition = "X: 0, Y: 0, Z: 0";

    [ObservableProperty]
    private string _selectedBlockState = "minecraft:stone";

    [ObservableProperty]
    private string _selectedBlockColorHex = "#808080";

    [ObservableProperty]
    private bool _showChunkBoundaries;

    [ObservableProperty]
    private bool _showCoordinates = true;

    public ObservableCollection<BlockPaletteItemViewModel> PaletteItems { get; } = [];
    public ObservableCollection<BlockPaletteItemViewModel> FilteredPaletteItems { get; } = [];

    public event EventHandler<string>? BlockFilterChanged;

    public void LoadFromScene(Scene3D scene)
    {
        PaletteItems.Clear();
        FilteredPaletteItems.Clear();
        SelectedPaletteItem = null;

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in scene.Graph.Nodes)
        {
            if (node.Object is not { } obj) continue;
            foreach (var grid in obj.VoxelGrids)
            {
                foreach (var voxel in grid.Voxels)
                {
                    string id = voxel.State?.GetFullIdentifier() ?? "minecraft:stone";
                    counts[id] = counts.GetValueOrDefault(id) + 1;
                }
            }
        }

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
                IsVisible = true
            };
            PaletteItems.Add(item);
        }

        TotalBlocks = total;
        UniqueBlockTypes = PaletteItems.Count;
        ApplyFilter();

        if (PaletteItems.Count > 0)
        {
            SelectBlock(PaletteItems[0]);
        }
    }

    partial void OnSearchFilterChanged(string value) => ApplyFilter();

    public void ApplyFilter()
    {
        FilteredPaletteItems.Clear();
        var q = SearchFilter?.Trim() ?? string.Empty;

        foreach (var item in PaletteItems)
        {
            if (string.IsNullOrEmpty(q) ||
                item.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                item.BlockId.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                FilteredPaletteItems.Add(item);
            }
        }
    }

    [RelayCommand]
    public void SelectBlock(BlockPaletteItemViewModel? item)
    {
        SelectedPaletteItem = item;
        if (item is null)
        {
            SelectedBlockInfo = "No block selected";
            return;
        }

        SelectedBlockInfo = $"{item.DisplayName} ({item.Count:N0} instances)";
        SelectedBlockState = item.BlockId;
        SelectedBlockColorHex = item.ColorHex;
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

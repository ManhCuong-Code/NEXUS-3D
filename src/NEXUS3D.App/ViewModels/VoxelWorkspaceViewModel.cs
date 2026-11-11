using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Rendering.Services;

namespace NEXUS3D.App.ViewModels;

public sealed class VoxelSwatchViewModel
{
    public required string ColorHex { get; init; }
    public required int Count { get; init; }
    public required string Label { get; init; }
}

/// <summary>
/// Specialized ViewModel for the Voxel Workspace (MagicaVoxel / Voxel JSON).
/// </summary>
public sealed partial class VoxelWorkspaceViewModel : ObservableObject
{
    [ObservableProperty]
    private int _totalVoxels;

    [ObservableProperty]
    private int _chunkCount = 1;

    [ObservableProperty]
    private float _voxelSize = 1.0f;

    [ObservableProperty]
    private bool _enableGreedyMeshing = true;

    [ObservableProperty]
    private string _selectedVoxelPosition = "X: 0, Y: 0, Z: 0";

    [ObservableProperty]
    private string _selectedVoxelColor = "#FFFFFF";

    [ObservableProperty]
    private string _selectedVoxelChunk = "Chunk (0, 0, 0)";

    [ObservableProperty]
    private string _voxelDimensions = "0 × 0 × 0";

    public ObservableCollection<VoxelSwatchViewModel> Swatches { get; } = [];

    public void LoadFromScene(Scene3D scene)
    {
        Swatches.Clear();

        int total = 0;
        var colorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in scene.Graph.Nodes)
        {
            if (node.Object is not { } obj) continue;
            foreach (var grid in obj.VoxelGrids)
            {
                var bounds = grid.Bounds;
                VoxelDimensions = $"{bounds.Width} × {bounds.Height} × {bounds.Depth}";

                foreach (var voxel in grid.Voxels)
                {
                    total++;
                    string blockId = voxel.State?.GetFullIdentifier() ?? "voxel";
                    var info = BlockMaterialRegistry.GetBlockInfo(blockId);
                    int r = (int)(info.Color.X * 255);
                    int g = (int)(info.Color.Y * 255);
                    int b = (int)(info.Color.Z * 255);
                    string hex = $"#{r:X2}{g:X2}{b:X2}";
                    colorCounts[hex] = colorCounts.GetValueOrDefault(hex) + 1;
                }
            }
        }

        TotalVoxels = total;
        ChunkCount = Math.Max(1, (int)Math.Ceiling(total / 4096.0));

        foreach (var (hex, count) in colorCounts.OrderByDescending(c => c.Value).Take(64))
        {
            Swatches.Add(new VoxelSwatchViewModel
            {
                ColorHex = hex,
                Count = count,
                Label = $"{count:N0} voxels"
            });
        }
    }

    [RelayCommand]
    public void ToggleGreedyMeshing()
    {
        EnableGreedyMeshing = !EnableGreedyMeshing;
    }

    [RelayCommand]
    public void OptimizeVoxels()
    {
        // Triggers greedy meshing or cull optimization
    }
}

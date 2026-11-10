using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.ViewModels;

public sealed partial class SceneHierarchyItemViewModel : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public RenderNodeType NodeType { get; }
    public int TriangleCount { get; }
    public int VoxelCount { get; }
    public ObservableCollection<SceneHierarchyItemViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded = true;

    public SceneHierarchyItemViewModel(RenderNode node)
    {
        Id = node.Id;
        Name = node.Name;
        NodeType = node.NodeType;
        TriangleCount = node.Meshes.Sum(m => m.TriangleCount);
        VoxelCount = node.VoxelChunks.Sum(c => c.VoxelCount);

        foreach (var child in node.Children)
        {
            Children.Add(new SceneHierarchyItemViewModel(child));
        }
    }
}

/// <summary>
/// ViewModel coordinating the scene hierarchy tree view and bidirectional node selection.
/// </summary>
public sealed partial class SceneHierarchyViewModel : ObservableObject
{
    private readonly IRenderService _renderService;

    public ObservableCollection<SceneHierarchyItemViewModel> RootNodes { get; } = [];

    [ObservableProperty]
    private SceneHierarchyItemViewModel? _selectedItem;

    public event EventHandler<string?>? NodeSelected;

    public SceneHierarchyViewModel(IRenderService renderService)
    {
        _renderService = renderService;
    }

    public void LoadScene(RenderScene scene)
    {
        RootNodes.Clear();
        if (scene is null) return;

        foreach (var node in scene.RootNodes)
        {
            RootNodes.Add(new SceneHierarchyItemViewModel(node));
        }
    }

    [RelayCommand]
    public void SelectNode(SceneHierarchyItemViewModel? item)
    {
        SelectedItem = item;
        _renderService.SelectNode(item?.Id);
        NodeSelected?.Invoke(this, item?.Id);
    }

    public void SelectById(string? nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            SelectedItem = null;
            return;
        }

        var found = FindRecursive(RootNodes, nodeId);
        if (found is not null)
        {
            SelectedItem = found;
            found.IsSelected = true;
        }
    }

    private static SceneHierarchyItemViewModel? FindRecursive(IEnumerable<SceneHierarchyItemViewModel> items, string id)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            var childFound = FindRecursive(item.Children, id);
            if (childFound is not null) return childFound;
        }

        return null;
    }
}

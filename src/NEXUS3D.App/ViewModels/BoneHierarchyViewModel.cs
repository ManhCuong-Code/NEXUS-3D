using System.Collections.ObjectModel;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.ViewModels;

public sealed partial class BoneHierarchyItemViewModel : ObservableObject
{
    public string Name { get; }
    public string? ParentName { get; }
    public Vector3 Pivot { get; }
    public Vector3 Rotation { get; }
    public ObservableCollection<BoneHierarchyItemViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded = true;

    public BoneHierarchyItemViewModel(RenderBone bone)
    {
        Name = bone.Name;
        ParentName = bone.ParentName;
        Pivot = bone.Pivot;
        Rotation = bone.Rotation;

        foreach (var child in bone.Children)
        {
            Children.Add(new BoneHierarchyItemViewModel(child));
        }
    }
}

/// <summary>
/// ViewModel managing the tree of skeleton bones, selection, and bone inspection.
/// </summary>
public sealed partial class BoneHierarchyViewModel : ObservableObject
{
    private readonly IRenderService _renderService;

    public ObservableCollection<BoneHierarchyItemViewModel> RootBones { get; } = [];

    [ObservableProperty]
    private BoneHierarchyItemViewModel? _selectedBone;

    public event EventHandler<string?>? BoneSelected;

    public BoneHierarchyViewModel(IRenderService renderService)
    {
        _renderService = renderService;
    }

    public void LoadSkeletons(IEnumerable<RenderBone> skeletons)
    {
        RootBones.Clear();
        foreach (var bone in skeletons)
        {
            RootBones.Add(new BoneHierarchyItemViewModel(bone));
        }
    }

    [RelayCommand]
    public void SelectBone(BoneHierarchyItemViewModel? item)
    {
        SelectedBone = item;
        _renderService.SelectBone(item?.Name);
        BoneSelected?.Invoke(this, item?.Name);
    }
}


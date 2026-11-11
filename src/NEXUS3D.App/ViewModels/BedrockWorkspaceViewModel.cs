using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;

namespace NEXUS3D.App.ViewModels;

public sealed partial class BedrockCubeItemViewModel : ObservableObject
{
    public required string Name { get; init; }
    public required string Origin { get; init; }
    public required string Size { get; init; }
    public required string Pivot { get; init; }
    public required string Rotation { get; init; }
    public required string Uv { get; init; }
    public bool Mirror { get; init; }
    public float Inflate { get; init; }
}

public sealed partial class BedrockBoneNodeViewModel : ObservableObject
{
    public required string Name { get; init; }
    public string? ParentName { get; init; }
    public required string Pivot { get; init; }
    public required string Rotation { get; init; }
    public ObservableCollection<BedrockBoneNodeViewModel> Children { get; } = [];
    public ObservableCollection<BedrockCubeItemViewModel> Cubes { get; } = [];

    public int TotalCubeCount => Cubes.Count + Children.Sum(c => c.TotalCubeCount);
}

/// <summary>
/// Specialized ViewModel managing Bedrock Geometry (.geo.json) Bone Hierarchy, Cube Inspection, and Kinematics.
/// </summary>
public sealed partial class BedrockWorkspaceViewModel : ObservableObject
{
    [ObservableProperty]
    private int _boneCount;

    [ObservableProperty]
    private int _cubeCount;

    [ObservableProperty]
    private BedrockBoneNodeViewModel? _selectedBone;

    [ObservableProperty]
    private BedrockCubeItemViewModel? _selectedCube;

    [ObservableProperty]
    private bool _showPivotGizmos = true;

    [ObservableProperty]
    private bool _showBoneConnections = true;

    [ObservableProperty]
    private bool _showUvOverlay;

    public ObservableCollection<BedrockBoneNodeViewModel> RootBones { get; } = [];

    public void LoadFromScene(Scene3D scene)
    {
        RootBones.Clear();
        SelectedBone = null;
        SelectedCube = null;

        int totalBones = 0;
        int totalCubes = 0;

        foreach (var skeleton in scene.Skeletons)
        {
            var boneMap = new Dictionary<Guid, BedrockBoneNodeViewModel>();

            foreach (var bone in skeleton.Bones)
            {
                totalBones++;
                var vm = new BedrockBoneNodeViewModel
                {
                    Name = bone.Name,
                    Pivot = $"{bone.Pivot.X:F1}, {bone.Pivot.Y:F1}, {bone.Pivot.Z:F1}",
                    Rotation = $"{bone.BindRotationDegrees.X:F1}°, {bone.BindRotationDegrees.Y:F1}°, {bone.BindRotationDegrees.Z:F1}°"
                };

                // Inspect locators or associated cubes
                foreach (var loc in bone.Locators)
                {
                    totalCubes++;
                    var rot = loc.RotationDegrees ?? NEXUS3D.Domain.Mathematics.Vector3D.Zero;
                    vm.Cubes.Add(new BedrockCubeItemViewModel
                    {
                        Name = loc.Name,
                        Origin = $"{loc.Offset.X:F1}, {loc.Offset.Y:F1}, {loc.Offset.Z:F1}",
                        Size = "1, 1, 1",
                        Pivot = $"{bone.Pivot.X:F1}, {bone.Pivot.Y:F1}, {bone.Pivot.Z:F1}",
                        Rotation = $"{rot.X:F1}°, {rot.Y:F1}°, {rot.Z:F1}°",
                        Uv = "0, 0",
                        Mirror = false,
                        Inflate = 0f
                    });
                }

                boneMap[bone.Id] = vm;
            }

            foreach (var bone in skeleton.Bones)
            {
                var vm = boneMap[bone.Id];
                if (bone.ParentBoneId.HasValue && boneMap.TryGetValue(bone.ParentBoneId.Value, out var parentVm))
                {
                    parentVm.Children.Add(vm);
                }
                else
                {
                    RootBones.Add(vm);
                }
            }
        }

        // If no explicit skeleton, check scene nodes
        if (RootBones.Count == 0)
        {
            foreach (var node in scene.Graph.Root.Children)
            {
                totalBones++;
                var p = node.LocalTransform.Position;
                var r = node.LocalTransform.Rotation;
                var bVm = new BedrockBoneNodeViewModel
                {
                    Name = node.Name,
                    Pivot = $"{p.X:F1}, {p.Y:F1}, {p.Z:F1}",
                    Rotation = "0°, 0°, 0°"
                };

                if (node.Object is { } obj)
                {
                    foreach (var m in obj.Meshes)
                    {
                        totalCubes++;
                        var min = m.Bounds.Min;
                        var max = m.Bounds.Max;
                        var size = max - min;
                        bVm.Cubes.Add(new BedrockCubeItemViewModel
                        {
                            Name = m.Name,
                            Origin = $"{min.X:F1}, {min.Y:F1}, {min.Z:F1}",
                            Size = $"{size.X:F1}, {size.Y:F1}, {size.Z:F1}",
                            Pivot = $"{p.X:F1}, {p.Y:F1}, {p.Z:F1}",
                            Rotation = "0°, 0°, 0°",
                            Uv = "0, 0",
                            Mirror = false,
                            Inflate = 0f
                        });
                    }
                }

                RootBones.Add(bVm);
            }
        }

        BoneCount = totalBones;
        CubeCount = totalCubes;

        if (RootBones.Count > 0)
        {
            SelectBone(RootBones[0]);
        }
    }

    [RelayCommand]
    public void SelectBone(BedrockBoneNodeViewModel? bone)
    {
        SelectedBone = bone;
        if (bone?.Cubes.Count > 0)
        {
            SelectedCube = bone.Cubes[0];
        }
        else
        {
            SelectedCube = null;
        }
    }

    [RelayCommand]
    public void SelectCube(BedrockCubeItemViewModel? cube)
    {
        SelectedCube = cube;
    }

    [RelayCommand]
    public void TogglePivots()
    {
        ShowPivotGizmos = !ShowPivotGizmos;
    }

    [RelayCommand]
    public void ToggleBoneConnections()
    {
        ShowBoneConnections = !ShowBoneConnections;
    }
}

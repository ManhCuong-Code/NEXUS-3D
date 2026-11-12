using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Observable item representing an individual Bedrock cuboid in the hierarchy and inspector.
/// </summary>
public sealed partial class BedrockCubeItemViewModel : ObservableObject
{
    public Guid? NodeId { get; init; }
    public string CubeId { get; init; } = string.Empty;
    public required string Name { get; init; }
    public string GeometryId { get; init; } = string.Empty;
    public string BoneId { get; init; } = string.Empty;
    public string BoneName { get; init; } = string.Empty;
    public string? ParentBone { get; init; }
    public required string Origin { get; init; }
    public required string Size { get; init; }
    public required string Pivot { get; init; }
    public required string Rotation { get; init; }
    public required string Uv { get; init; }
    public bool Mirror { get; init; }
    public float Inflate { get; init; }
    public string Material { get; init; } = "default";

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Observable node representing a Bedrock bone in the skeleton hierarchy with child bones and cuboids.
/// </summary>
public sealed partial class BedrockBoneNodeViewModel : ObservableObject
{
    public Guid? NodeId { get; init; }
    public required string Name { get; init; }
    public string? ParentName { get; init; }
    public required string Pivot { get; init; }
    public required string Rotation { get; init; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded = true;

    public ObservableCollection<BedrockBoneNodeViewModel> Children { get; } = [];
    public ObservableCollection<BedrockCubeItemViewModel> Cubes { get; } = [];

    public int TotalCubeCount => Cubes.Count + Children.Sum(c => c.TotalCubeCount);
}

/// <summary>
/// Observable node representing a Bedrock geometry definition (e.g. geometry.nexus_guardian).
/// </summary>
public sealed partial class BedrockGeometryNodeViewModel : ObservableObject
{
    public required string Identifier { get; init; }
    public string FormatVersion { get; init; } = "1.12.0";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded = true;

    public ObservableCollection<BedrockBoneNodeViewModel> Bones { get; } = [];
    public ObservableCollection<BedrockBoneNodeViewModel> Children => Bones;

    public int TotalBoneCount => Bones.Count + Bones.Sum(b => b.Children.Count);
    public int TotalCubeCount => Bones.Sum(b => b.TotalCubeCount);
}

/// <summary>
/// Specialized ViewModel managing Bedrock Geometry (.geo.json) Bone Hierarchy, Cube Inspection, and Kinematics.
/// Builds an observable tree structure: Geometry -> Bone -> Cubes with bidirectional selection synchronization
/// between the 3D Viewport and Object Inspector.
/// </summary>
public sealed partial class BedrockWorkspaceViewModel : ObservableObject
{
    private ObjectInspectorViewModel? _inspector;
    private Scene3D? _currentScene;

    [ObservableProperty]
    private int _boneCount;

    [ObservableProperty]
    private int _cubeCount;

    [ObservableProperty]
    private BedrockGeometryNodeViewModel? _selectedGeometry;

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

    public ObservableCollection<BedrockGeometryNodeViewModel> Geometries { get; } = [];
    public ObservableCollection<BedrockBoneNodeViewModel> RootBones { get; } = [];

    /// <summary>
    /// Event raised when an individual cube is selected, passing its SceneNode ID string to notify Viewport.
    /// </summary>
    public event EventHandler<string?>? CubeHighlightRequested;

    /// <summary>
    /// Event raised when an individual cube is selected, passing its SceneNode ID string to notify Viewport.
    /// </summary>
    public event EventHandler<string?>? CubeSelected;

    /// <summary>
    /// Event raised when the selected cube item changes.
    /// </summary>
    public event EventHandler<BedrockCubeItemViewModel?>? CubeSelectionChanged;

    /// <summary>
    /// Optional direct delegate to trigger node selection in the active 3D Viewport.
    /// </summary>
    public Action<string?>? ViewportNodeSelector { get; set; }

    public ObjectInspectorViewModel? Inspector
    {
        get => _inspector;
        set => _inspector = value;
    }

    public BedrockWorkspaceViewModel() : this(null)
    {
    }

    public BedrockWorkspaceViewModel(ObjectInspectorViewModel? inspector = null)
    {
        _inspector = inspector;
    }

    /// <summary>
    /// Loads and builds the observable Geometry -> Bone -> Cubes hierarchy from a Universal Scene Model (<see cref="Scene3D"/>).
    /// </summary>
    public void LoadFromScene(Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _currentScene = scene;

        Geometries.Clear();
        RootBones.Clear();
        SelectedGeometry = null;
        SelectedBone = null;
        SelectedCube = null;

        int totalBones = 0;
        int totalCubes = 0;

        // Collect all SceneNodes by name for fast lookup
        var sceneNodesByName = new Dictionary<string, SceneNode>(StringComparer.Ordinal);
        foreach (var node in scene.Graph.Traverse())
        {
            sceneNodesByName.TryAdd(node.Name, node);
        }

        // 1. Try reading from preserved Bedrock JSON document snapshot
        BedrockGeometryDocument? document = null;
        if (scene.Metadata.Extensions.TryGetValue(BedrockGeometryToSceneConverter.ExtensionNamespace, out var ext) &&
            ext.Values.TryGetValue("documentSnapshot", out var snapElem))
        {
            try
            {
                var parser = new BedrockGeometryParser();
                var parseResult = parser.Parse(snapElem.GetRawText(), new BedrockGeometryReadOptions { StrictMode = false });
                document = parseResult.Document;
            }
            catch
            {
                document = null;
            }
        }

        if (document is not null && document.Geometries.Count > 0)
        {
            foreach (var geo in document.Geometries)
            {
                string geoId = !string.IsNullOrWhiteSpace(geo.Description.Identifier)
                    ? geo.Description.Identifier
                    : "geometry.model";

                var geoVm = new BedrockGeometryNodeViewModel
                {
                    Identifier = geoId,
                    FormatVersion = document.FormatVersion
                };

                var boneVmMap = new Dictionary<string, BedrockBoneNodeViewModel>(StringComparer.Ordinal);

                for (int bIdx = 0; bIdx < geo.Bones.Count; bIdx++)
                {
                    var bone = geo.Bones[bIdx];
                    totalBones++;
                    sceneNodesByName.TryGetValue(bone.Name, out var boneNode);

                    var boneVm = new BedrockBoneNodeViewModel
                    {
                        NodeId = boneNode?.Id,
                        Name = bone.Name,
                        ParentName = bone.Parent,
                        Pivot = $"{bone.Pivot.X:F1}, {bone.Pivot.Y:F1}, {bone.Pivot.Z:F1}",
                        Rotation = $"{bone.Rotation.X:F1}°, {bone.Rotation.Y:F1}°, {bone.Rotation.Z:F1}°"
                    };

                    for (int cIdx = 0; cIdx < bone.Cubes.Count; cIdx++)
                    {
                        totalCubes++;
                        var cube = bone.Cubes[cIdx];
                        string cubeName = $"{bone.Name}_cube_{cIdx}";
                        if (cube.ExtensionData.TryGet("name", out var extNameElem) &&
                            extNameElem.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(extNameElem.GetString()))
                        {
                            cubeName = extNameElem.GetString()!;
                        }

                        // Match child cube node under owning bone SceneNode
                        SceneNode? cubeNode = null;
                        if (boneNode is not null)
                        {
                            cubeNode = boneNode.Children.FirstOrDefault(c => c.Name == cubeName)
                                       ?? boneNode.Children.ElementAtOrDefault(cIdx);
                        }

                        var cubePivot = cube.Pivot ?? bone.Pivot;
                        var cubeRot = cube.Rotation ?? Vector3D.Zero;
                        double inflate = cube.Inflate ?? bone.Inflate ?? 0.0;
                        bool mirror = cube.Mirror ?? bone.Mirror ?? false;
                        string uv = cube.Uv?.Mode == BedrockUvMode.BoxUv
                            ? $"{cube.Uv.BoxUvOffset.X:F1}, {cube.Uv.BoxUvOffset.Y:F1}"
                            : (cube.Uv?.Mode == BedrockUvMode.PerFaceUv ? "PerFace" : "0, 0");

                        var cubeVm = new BedrockCubeItemViewModel
                        {
                            NodeId = cubeNode?.Id,
                            CubeId = cubeNode?.Id.ToString() ?? Guid.NewGuid().ToString(),
                            Name = cubeName,
                            GeometryId = geoId,
                            BoneId = boneNode?.Id.ToString() ?? string.Empty,
                            BoneName = bone.Name,
                            ParentBone = bone.Parent,
                            Origin = $"{cube.Origin.X:F1}, {cube.Origin.Y:F1}, {cube.Origin.Z:F1}",
                            Size = $"{cube.Size.X:F1}, {cube.Size.Y:F1}, {cube.Size.Z:F1}",
                            Pivot = $"{cubePivot.X:F1}, {cubePivot.Y:F1}, {cubePivot.Z:F1}",
                            Rotation = $"{cubeRot.X:F1}°, {cubeRot.Y:F1}°, {cubeRot.Z:F1}°",
                            Uv = uv,
                            Mirror = mirror,
                            Inflate = (float)inflate,
                            Material = bone.Material ?? "default"
                        };

                        boneVm.Cubes.Add(cubeVm);
                    }

                    boneVmMap[bone.Name] = boneVm;
                }

                // Wire bone hierarchy in topological order
                foreach (var bone in geo.Bones)
                {
                    var boneVm = boneVmMap[bone.Name];
                    if (!string.IsNullOrEmpty(bone.Parent) && boneVmMap.TryGetValue(bone.Parent, out var parentVm))
                    {
                        parentVm.Children.Add(boneVm);
                    }
                    else
                    {
                        geoVm.Bones.Add(boneVm);
                        RootBones.Add(boneVm);
                    }
                }

                Geometries.Add(geoVm);
            }
        }
        else
        {
            // 2. Fallback for scenes without Bedrock snapshot (e.g. Skeletons or direct SceneNodes)
            var geoVm = new BedrockGeometryNodeViewModel
            {
                Identifier = string.IsNullOrWhiteSpace(scene.Name) ? "geometry.model" : scene.Name,
                FormatVersion = "1.12.0"
            };

            if (scene.Skeletons.Count > 0)
            {
                foreach (var skeleton in scene.Skeletons)
                {
                    var boneMap = new Dictionary<Guid, BedrockBoneNodeViewModel>();
                    foreach (var bone in skeleton.Bones)
                    {
                        totalBones++;
                        sceneNodesByName.TryGetValue(bone.Name, out var boneNode);

                        var vm = new BedrockBoneNodeViewModel
                        {
                            NodeId = boneNode?.Id,
                            Name = bone.Name,
                            Pivot = $"{bone.Pivot.X:F1}, {bone.Pivot.Y:F1}, {bone.Pivot.Z:F1}",
                            Rotation = $"{bone.BindRotationDegrees.X:F1}°, {bone.BindRotationDegrees.Y:F1}°, {bone.BindRotationDegrees.Z:F1}°"
                        };

                        if (boneNode is not null)
                        {
                            foreach (var childNode in boneNode.Children)
                            {
                                if (skeleton.Bones.Any(b => b.Name == childNode.Name)) continue;

                                totalCubes++;
                                var props = childNode.Object?.Properties;
                                string orig = GetPropString(props, "Origin", "bedrock.origin") ?? "0, 0, 0";
                                string sz = GetPropString(props, "Size", "bedrock.size") ?? "1, 1, 1";
                                string piv = GetPropString(props, "Pivot", "bedrock.pivot") ?? vm.Pivot;
                                string rot = GetPropString(props, "Rotation", "bedrock.rotation") ?? "0°, 0°, 0°";
                                string uv = GetPropString(props, "UV", "bedrock.uv") ?? "0, 0";

                                vm.Cubes.Add(new BedrockCubeItemViewModel
                                {
                                    NodeId = childNode.Id,
                                    CubeId = childNode.Id.ToString(),
                                    Name = childNode.Name,
                                    GeometryId = geoVm.Identifier,
                                    BoneId = boneNode.Id.ToString(),
                                    BoneName = bone.Name,
                                    ParentBone = null,
                                    Origin = orig,
                                    Size = sz,
                                    Pivot = piv,
                                    Rotation = rot,
                                    Uv = uv,
                                    Mirror = false,
                                    Inflate = 0f,
                                    Material = "default"
                                });
                            }
                        }

                        if (vm.Cubes.Count == 0)
                        {
                            foreach (var loc in bone.Locators)
                            {
                                totalCubes++;
                                var rot = loc.RotationDegrees ?? Vector3D.Zero;
                                vm.Cubes.Add(new BedrockCubeItemViewModel
                                {
                                    CubeId = Guid.NewGuid().ToString(),
                                    Name = loc.Name,
                                    GeometryId = geoVm.Identifier,
                                    BoneId = string.Empty,
                                    BoneName = bone.Name,
                                    Origin = $"{loc.Offset.X:F1}, {loc.Offset.Y:F1}, {loc.Offset.Z:F1}",
                                    Size = "1, 1, 1",
                                    Pivot = $"{bone.Pivot.X:F1}, {bone.Pivot.Y:F1}, {bone.Pivot.Z:F1}",
                                    Rotation = $"{rot.X:F1}°, {rot.Y:F1}°, {rot.Z:F1}°",
                                    Uv = "0, 0",
                                    Mirror = false,
                                    Inflate = 0f,
                                    Material = "default"
                                });
                            }
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
                            geoVm.Bones.Add(vm);
                            RootBones.Add(vm);
                        }
                    }
                }
            }
            else
            {
                // Fallback for non-skeletal scene nodes
                foreach (var node in scene.Graph.Root.Children)
                {
                    totalBones++;
                    var p = node.LocalTransform.Position;
                    var bVm = new BedrockBoneNodeViewModel
                    {
                        NodeId = node.Id,
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
                                NodeId = node.Id,
                                CubeId = m.Id.ToString(),
                                Name = m.Name,
                                GeometryId = geoVm.Identifier,
                                BoneId = node.Id.ToString(),
                                BoneName = node.Name,
                                Origin = $"{min.X:F1}, {min.Y:F1}, {min.Z:F1}",
                                Size = $"{size.X:F1}, {size.Y:F1}, {size.Z:F1}",
                                Pivot = $"{p.X:F1}, {p.Y:F1}, {p.Z:F1}",
                                Rotation = "0°, 0°, 0°",
                                Uv = "0, 0",
                                Mirror = false,
                                Inflate = 0f,
                                Material = "default"
                            });
                        }
                    }

                    geoVm.Bones.Add(bVm);
                    RootBones.Add(bVm);
                }
            }

            Geometries.Add(geoVm);
        }

        BoneCount = totalBones;
        CubeCount = totalCubes;

        if (Geometries.Count > 0)
        {
            SelectedGeometry = Geometries[0];
        }

        if (RootBones.Count > 0)
        {
            SelectBone(RootBones[0]);
        }
    }

    [RelayCommand]
    public void SelectBone(BedrockBoneNodeViewModel? bone)
    {
        SelectedBone = bone;
        if (bone is null) return;

        bone.IsSelected = true;

        if (bone.Cubes.Count > 0)
        {
            SelectCube(bone.Cubes[0]);
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
        if (cube is null) return;

        // Update selection flags across all cubes
        foreach (var c in GetAllCubes())
        {
            c.IsSelected = ReferenceEquals(c, cube);
        }

        // Notify Viewport via events and delegate
        string? targetNodeId = cube.NodeId?.ToString();
        if (!string.IsNullOrEmpty(targetNodeId))
        {
            ViewportNodeSelector?.Invoke(targetNodeId);
            CubeHighlightRequested?.Invoke(this, targetNodeId);
            CubeSelected?.Invoke(this, targetNodeId);
        }

        CubeSelectionChanged?.Invoke(this, cube);

        // Display full cube properties in Object Inspector
        if (_inspector is not null && _currentScene is not null && cube.NodeId.HasValue)
        {
            var node = _currentScene.Graph.FindNode(cube.NodeId.Value);
            if (node is not null)
            {
                _inspector.InspectUsmNode(node, _currentScene);
            }
        }
    }

    /// <summary>
    /// Highlights and selects a bone or cube in the hierarchy by its SceneNode ID or name
    /// (e.g. when selected in the 3D Viewport), expanding the owning tree branch and updating the Inspector.
    /// </summary>
    public void SelectNodeById(string? nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            SelectedCube = null;
            return;
        }

        foreach (var geo in Geometries)
        {
            foreach (var bone in GetAllBones(geo.Bones))
            {
                var matchingCube = bone.Cubes.FirstOrDefault(c =>
                    string.Equals(c.NodeId?.ToString(), nodeId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.CubeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name, nodeId, StringComparison.OrdinalIgnoreCase));

                if (matchingCube is not null)
                {
                    SelectedGeometry = geo;
                    SelectedBone = bone;
                    SelectedCube = matchingCube;

                    geo.IsExpanded = true;
                    bone.IsExpanded = true;
                    bone.IsSelected = true;

                    foreach (var c in GetAllCubes())
                    {
                        c.IsSelected = ReferenceEquals(c, matchingCube);
                    }

                    if (_inspector is not null && _currentScene is not null && matchingCube.NodeId.HasValue)
                    {
                        var node = _currentScene.Graph.FindNode(matchingCube.NodeId.Value);
                        if (node is not null)
                        {
                            _inspector.InspectUsmNode(node, _currentScene);
                        }
                    }
                    return;
                }

                if (string.Equals(bone.NodeId?.ToString(), nodeId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(bone.Name, nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedGeometry = geo;
                    SelectedBone = bone;
                    geo.IsExpanded = true;
                    bone.IsExpanded = true;
                    bone.IsSelected = true;

                    if (bone.Cubes.Count > 0)
                    {
                        SelectCube(bone.Cubes[0]);
                    }
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Alias for <see cref="SelectNodeById(string?)"/> specifically for cube selection from Viewport.
    /// </summary>
    public void SelectCubeByNodeId(string? nodeId) => SelectNodeById(nodeId);

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

    private IEnumerable<BedrockBoneNodeViewModel> GetAllBones(IEnumerable<BedrockBoneNodeViewModel> rootBones)
    {
        foreach (var bone in rootBones)
        {
            yield return bone;
            foreach (var child in GetAllBones(bone.Children))
            {
                yield return child;
            }
        }
    }

    private IEnumerable<BedrockCubeItemViewModel> GetAllCubes()
    {
        foreach (var geo in Geometries)
        {
            foreach (var bone in GetAllBones(geo.Bones))
            {
                foreach (var cube in bone.Cubes)
                {
                    yield return cube;
                }
            }
        }

        foreach (var bone in GetAllBones(RootBones))
        {
            foreach (var cube in bone.Cubes)
            {
                yield return cube;
            }
        }
    }

    private static string? GetPropString(Dictionary<string, JsonElement>? props, string key1, string key2)
    {
        if (props is null) return null;
        if (props.TryGetValue(key1, out var elem) || props.TryGetValue(key2, out elem))
        {
            if (elem.ValueKind == JsonValueKind.String) return elem.GetString();
            if (elem.ValueKind == JsonValueKind.Array)
            {
                var items = elem.EnumerateArray().Select(e => e.ToString()).ToArray();
                return string.Join(", ", items);
            }
            return elem.ToString();
        }
        return null;
    }
}

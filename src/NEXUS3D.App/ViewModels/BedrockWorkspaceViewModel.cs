using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Observable item representing an individual Bedrock cuboid in the hierarchy and inspector.
/// Supports interactive editing of Origin, Size, Pivot, Rotation, Inflate, UV, Mirror, and bind-pose reset.
/// </summary>
public sealed partial class BedrockCubeItemViewModel : ObservableObject
{
    private bool _isUpdating;

    public Guid? NodeId { get; init; }
    public string CubeId { get; init; } = string.Empty;
    public required string Name { get; init; }
    public string GeometryId { get; init; } = string.Empty;
    public string BoneId { get; init; } = string.Empty;
    public string BoneName { get; init; } = string.Empty;
    public string? ParentBone { get; init; }

    [ObservableProperty]
    private string _origin = string.Empty;

    [ObservableProperty]
    private string _size = string.Empty;

    [ObservableProperty]
    private string _pivot = string.Empty;

    [ObservableProperty]
    private string _rotation = string.Empty;

    [ObservableProperty]
    private string _uv = string.Empty;

    [ObservableProperty]
    private bool _mirror;

    [ObservableProperty]
    private float _inflate;

    [ObservableProperty]
    private string _material = "default";

    [ObservableProperty]
    private bool _isSelected;

    // Original bind-pose cube values for ResetTransform
    public string OriginalOrigin { get; set; } = string.Empty;
    public string OriginalSize { get; set; } = string.Empty;
    public string OriginalPivot { get; set; } = string.Empty;
    public string OriginalRotation { get; set; } = string.Empty;
    public string OriginalUv { get; set; } = string.Empty;
    public float OriginalInflate { get; set; }
    public bool OriginalMirror { get; set; }

    public BedrockCube? SourceCube { get; set; }
    public BedrockBone? SourceBone { get; set; }
    public BedrockGeometryDescription? SourceDescription { get; set; }
    public Guid? MaterialId { get; set; }

    internal BedrockWorkspaceViewModel? Workspace { get; set; }

    partial void OnOriginChanged(string value) => OnPropertyEdited();
    partial void OnSizeChanged(string value) => OnPropertyEdited();
    partial void OnPivotChanged(string value) => OnPropertyEdited();
    partial void OnRotationChanged(string value) => OnPropertyEdited();
    partial void OnUvChanged(string value) => OnPropertyEdited();
    partial void OnInflateChanged(float value) => OnPropertyEdited();
    partial void OnMirrorChanged(bool value) => OnPropertyEdited();

    private void OnPropertyEdited()
    {
        if (_isUpdating) return;
        Workspace?.RecalculateCubeMesh(this);
    }

    /// <summary>
    /// Restores original bind-pose cube values and triggers immediate mesh recalculation and Viewport refresh.
    /// </summary>
    [RelayCommand]
    public void ResetTransform()
    {
        _isUpdating = true;
        try
        {
            Origin = OriginalOrigin;
            Size = OriginalSize;
            Pivot = OriginalPivot;
            Rotation = OriginalRotation;
            Uv = OriginalUv;
            Inflate = OriginalInflate;
            Mirror = OriginalMirror;
        }
        finally
        {
            _isUpdating = false;
        }

        Workspace?.RecalculateCubeMesh(this);
    }
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

    public IEnumerable<object> OutlinerChildren => Children.Cast<object>().Concat(Cubes.Cast<object>());

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
/// Specialized ViewModel managing Bedrock Geometry (.geo.json) Bone Hierarchy, Cube Inspection, Kinematics,
/// Geometry Diagnostics, and Interactive Cube Property Editing.
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

    // Full 14 Geometry Diagnostics
    [ObservableProperty]
    private int _totalGeometries;

    [ObservableProperty]
    private int _totalBones;

    [ObservableProperty]
    private int _totalCubes;

    [ObservableProperty]
    private int _validCubes;

    [ObservableProperty]
    private int _errorCubes;

    [ObservableProperty]
    private int _meshes;

    [ObservableProperty]
    private int _faces;

    [ObservableProperty]
    private int _vertices;

    [ObservableProperty]
    private int _triangles;

    [ObservableProperty]
    private int _cubesWithUv;

    [ObservableProperty]
    private int _cubesMissingUv;

    [ObservableProperty]
    private int _bonesWithParent;

    [ObservableProperty]
    private int _texturesFound;

    [ObservableProperty]
    private int _missingTextures;

    [ObservableProperty]
    private bool _hasMissingTextureWarning;

    [ObservableProperty]
    private string _missingTextureWarningMessage = "Missing Texture: Không tìm thấy ảnh Skin tương ứng với Geometry.";

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
    /// Event raised when a cube's mesh vertices are recalculated after editing or resetting properties.
    /// </summary>
    public event EventHandler<BedrockCubeItemViewModel?>? CubeMeshUpdated;

    /// <summary>
    /// Delegate to notify the 3D Viewport to refresh immediately when geometry or properties change.
    /// </summary>
    public Action? ViewportRefresher { get; set; }

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
    /// Loads and builds the observable Geometry -> Bone -> Cubes hierarchy and Diagnostics from a Universal Scene Model (<see cref="Scene3D"/>).
    /// </summary>
    public void LoadFromScene(Scene3D scene) => LoadFromScene(scene, null);

    /// <summary>
    /// Loads and builds the observable Geometry -> Bone -> Cubes hierarchy and Diagnostics from a Universal Scene Model (<see cref="Scene3D"/>) and source file path.
    /// </summary>
    public void LoadFromScene(Scene3D scene, string? filePath)
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
        int validCubes = 0;
        int errorCubes = 0;
        int cubesWithUv = 0;
        int bonesWithParent = 0;

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
                    if (!string.IsNullOrEmpty(bone.Parent))
                    {
                        bonesWithParent++;
                    }

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
                        if (cube.HasValidOrigin && cube.HasValidSize)
                        {
                            validCubes++;
                        }
                        else
                        {
                            errorCubes++;
                        }

                        if (cube.Uv is not null)
                        {
                            cubesWithUv++;
                        }

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

                        string origStr = $"{cube.Origin.X:F1}, {cube.Origin.Y:F1}, {cube.Origin.Z:F1}";
                        string sizeStr = $"{cube.Size.X:F1}, {cube.Size.Y:F1}, {cube.Size.Z:F1}";
                        string pivStr = $"{cubePivot.X:F1}, {cubePivot.Y:F1}, {cubePivot.Z:F1}";
                        string rotStr = $"{cubeRot.X:F1}°, {cubeRot.Y:F1}°, {cubeRot.Z:F1}°";

                        var cubeVm = new BedrockCubeItemViewModel
                        {
                            NodeId = cubeNode?.Id,
                            CubeId = cubeNode?.Id.ToString() ?? Guid.NewGuid().ToString(),
                            Name = cubeName,
                            GeometryId = geoId,
                            BoneId = boneNode?.Id.ToString() ?? string.Empty,
                            BoneName = bone.Name,
                            ParentBone = bone.Parent,
                            Origin = origStr,
                            Size = sizeStr,
                            Pivot = pivStr,
                            Rotation = rotStr,
                            Uv = uv,
                            Mirror = mirror,
                            Inflate = (float)inflate,
                            Material = bone.Material ?? "default"
                        };

                        cubeVm.OriginalOrigin = origStr;
                        cubeVm.OriginalSize = sizeStr;
                        cubeVm.OriginalPivot = pivStr;
                        cubeVm.OriginalRotation = rotStr;
                        cubeVm.OriginalUv = uv;
                        cubeVm.OriginalInflate = (float)inflate;
                        cubeVm.OriginalMirror = mirror;

                        cubeVm.SourceCube = cube;
                        cubeVm.SourceBone = bone;
                        cubeVm.SourceDescription = geo.Description;
                        cubeVm.MaterialId = cubeNode?.Object?.Meshes.FirstOrDefault()?.Faces.FirstOrDefault()?.MaterialId;
                        cubeVm.Workspace = this;

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
                        if (bone.ParentBoneId.HasValue) bonesWithParent++;

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
                                validCubes++;
                                cubesWithUv++;
                                var props = childNode.Object?.Properties;
                                string orig = GetPropString(props, "Origin", "bedrock.origin") ?? "0, 0, 0";
                                string sz = GetPropString(props, "Size", "bedrock.size") ?? "1, 1, 1";
                                string piv = GetPropString(props, "Pivot", "bedrock.pivot") ?? vm.Pivot;
                                string rot = GetPropString(props, "Rotation", "bedrock.rotation") ?? "0°, 0°, 0°";
                                string uv = GetPropString(props, "UV", "bedrock.uv") ?? "0, 0";

                                var cubeVm = new BedrockCubeItemViewModel
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
                                };

                                cubeVm.OriginalOrigin = orig;
                                cubeVm.OriginalSize = sz;
                                cubeVm.OriginalPivot = piv;
                                cubeVm.OriginalRotation = rot;
                                cubeVm.OriginalUv = uv;
                                cubeVm.Workspace = this;

                                vm.Cubes.Add(cubeVm);
                            }
                        }

                        if (vm.Cubes.Count == 0)
                        {
                            foreach (var loc in bone.Locators)
                            {
                                totalCubes++;
                                validCubes++;
                                cubesWithUv++;
                                var rot = loc.RotationDegrees ?? Vector3D.Zero;
                                var cubeVm = new BedrockCubeItemViewModel
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
                                };

                                cubeVm.OriginalOrigin = cubeVm.Origin;
                                cubeVm.OriginalSize = cubeVm.Size;
                                cubeVm.OriginalPivot = cubeVm.Pivot;
                                cubeVm.OriginalRotation = cubeVm.Rotation;
                                cubeVm.OriginalUv = cubeVm.Uv;
                                cubeVm.Workspace = this;

                                vm.Cubes.Add(cubeVm);
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
                            validCubes++;
                            cubesWithUv++;
                            var min = m.Bounds.Min;
                            var max = m.Bounds.Max;
                            var size = max - min;
                            var cubeVm = new BedrockCubeItemViewModel
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
                            };

                            cubeVm.OriginalOrigin = cubeVm.Origin;
                            cubeVm.OriginalSize = cubeVm.Size;
                            cubeVm.OriginalPivot = cubeVm.Pivot;
                            cubeVm.OriginalRotation = cubeVm.Rotation;
                            cubeVm.OriginalUv = cubeVm.Uv;
                            cubeVm.Workspace = this;

                            bVm.Cubes.Add(cubeVm);
                        }
                    }

                    geoVm.Bones.Add(bVm);
                    RootBones.Add(bVm);
                }
            }

            Geometries.Add(geoVm);
        }

        // Expose full Geometry Diagnostics
        PopulateDiagnostics(scene, document, filePath, totalBones, totalCubes, validCubes, errorCubes, cubesWithUv, bonesWithParent);

        if (Geometries.Count > 0)
        {
            SelectedGeometry = Geometries[0];
        }

        if (RootBones.Count > 0)
        {
            SelectBone(RootBones[0]);
        }
    }

    private void PopulateDiagnostics(
        Scene3D scene,
        BedrockGeometryDocument? document,
        string? filePath,
        int totalBones,
        int totalCubes,
        int validCubes,
        int errorCubes,
        int cubesWithUv,
        int bonesWithParent)
    {
        TotalGeometries = 0;
        TotalBones = 0;
        TotalCubes = 0;
        ValidCubes = 0;
        ErrorCubes = 0;
        Meshes = 0;
        Faces = 0;
        Vertices = 0;
        Triangles = 0;
        CubesWithUv = 0;
        CubesMissingUv = 0;
        BonesWithParent = 0;
        TexturesFound = 0;
        MissingTextures = 0;
        HasMissingTextureWarning = false;
        MissingTextureWarningMessage = "Missing Texture: Không tìm thấy ảnh Skin tương ứng với Geometry.";

        bool hasDiagExt = scene.Metadata.Extensions.TryGetValue("bedrock:diagnostics", out var diagExt);
        if (hasDiagExt && diagExt != null)
        {
            if (diagExt.Values.TryGetValue("totalGeometries", out var tg) && tg.ValueKind == JsonValueKind.Number) TotalGeometries = tg.GetInt32();
            if (diagExt.Values.TryGetValue("totalBones", out var tb) && tb.ValueKind == JsonValueKind.Number) TotalBones = tb.GetInt32();
            if (diagExt.Values.TryGetValue("totalCubes", out var tc) && tc.ValueKind == JsonValueKind.Number) TotalCubes = tc.GetInt32();
            if (diagExt.Values.TryGetValue("validCubes", out var vc) && vc.ValueKind == JsonValueKind.Number) ValidCubes = vc.GetInt32();
            if (diagExt.Values.TryGetValue("errorCubes", out var ec) && ec.ValueKind == JsonValueKind.Number) ErrorCubes = ec.GetInt32();
            if (diagExt.Values.TryGetValue("meshes", out var m) && m.ValueKind == JsonValueKind.Number) Meshes = m.GetInt32();
            if (diagExt.Values.TryGetValue("faces", out var f) && f.ValueKind == JsonValueKind.Number) Faces = f.GetInt32();
            if (diagExt.Values.TryGetValue("vertices", out var v) && v.ValueKind == JsonValueKind.Number) Vertices = v.GetInt32();
            if (diagExt.Values.TryGetValue("triangles", out var tr) && tr.ValueKind == JsonValueKind.Number) Triangles = tr.GetInt32();
            if (diagExt.Values.TryGetValue("cubesWithUv", out var cuv) && cuv.ValueKind == JsonValueKind.Number) CubesWithUv = cuv.GetInt32();
            if (diagExt.Values.TryGetValue("bonesWithParent", out var bwp) && bwp.ValueKind == JsonValueKind.Number) BonesWithParent = bwp.GetInt32();
            if (diagExt.Values.TryGetValue("cubesMissingUv", out var cmuv) && cmuv.ValueKind == JsonValueKind.Number) CubesMissingUv = cmuv.GetInt32();
            if (diagExt.Values.TryGetValue("texturesFound", out var tf) && tf.ValueKind == JsonValueKind.Number) TexturesFound = tf.GetInt32();
            if (diagExt.Values.TryGetValue("missingTextures", out var mt) && mt.ValueKind == JsonValueKind.Number) MissingTextures = mt.GetInt32();
            if (diagExt.Values.TryGetValue("hasMissingTextureWarning", out var hmtw) && (hmtw.ValueKind is JsonValueKind.True or JsonValueKind.False)) HasMissingTextureWarning = hmtw.GetBoolean();
            if (diagExt.Values.TryGetValue("missingTextureWarningMessage", out var mtwm) && mtwm.ValueKind == JsonValueKind.String) MissingTextureWarningMessage = mtwm.GetString() ?? MissingTextureWarningMessage;
        }

        var sceneMeshes = scene.Graph.Traverse()
            .Select(n => n.Object)
            .Where(obj => obj is not null)
            .SelectMany(obj => obj!.Meshes)
            .ToList();

        if (TotalGeometries == 0) TotalGeometries = document?.Geometries.Count ?? Math.Max(1, Geometries.Count);
        if (TotalBones == 0) TotalBones = totalBones;
        if (TotalCubes == 0) TotalCubes = totalCubes;
        if (ValidCubes == 0 && TotalCubes > 0) ValidCubes = validCubes > 0 ? validCubes : TotalCubes - errorCubes;
        if (ErrorCubes == 0 && TotalCubes > ValidCubes) ErrorCubes = TotalCubes - ValidCubes;
        if (Meshes == 0) Meshes = sceneMeshes.Count > 0 ? sceneMeshes.Count : totalCubes;
        if (Faces == 0) Faces = sceneMeshes.Sum(m => m.Faces.Count);
        if (Vertices == 0) Vertices = sceneMeshes.Sum(m => m.Vertices.Count);
        if (Triangles == 0) Triangles = sceneMeshes.Sum(m => m.Faces.Sum(fc => Math.Max(0, fc.VertexIndices.Count - 2)));
        if (CubesWithUv == 0 && TotalCubes > 0) CubesWithUv = cubesWithUv > 0 ? cubesWithUv : TotalCubes;
        if (CubesMissingUv == 0) CubesMissingUv = Math.Max(0, TotalCubes - CubesWithUv);
        if (BonesWithParent == 0) BonesWithParent = bonesWithParent;

        BoneCount = TotalBones;
        CubeCount = TotalCubes;

        // Texture discovery & validation
        if (TexturesFound == 0 && MissingTextures == 0)
        {
            bool hasTexture = CheckHasSkinTexture(scene, filePath, document);
            if (hasTexture)
            {
                TexturesFound = 1;
                MissingTextures = 0;
                HasMissingTextureWarning = false;
            }
            else
            {
                TexturesFound = 0;
                MissingTextures = Math.Max(1, TotalGeometries);
                HasMissingTextureWarning = true;
                MissingTextureWarningMessage = "Missing Texture: Không tìm thấy ảnh Skin tương ứng với Geometry.";
            }
        }
        else
        {
            HasMissingTextureWarning = MissingTextures > 0;
        }
    }

    private static bool CheckHasSkinTexture(Scene3D scene, string? explicitFilePath, BedrockGeometryDocument? document)
    {
        // 1. Check if any material has assigned diffuse/albedo texture
        foreach (var mat in scene.Materials.Materials)
        {
            if (mat.TextureSlots.TryGetValue("Albedo", out var slot) ||
                mat.TextureSlots.TryGetValue("Diffuse", out slot) ||
                mat.TextureSlots.TryGetValue("map_Kd", out slot))
            {
                if (!string.IsNullOrWhiteSpace(slot.Channel)) return true;
            }

            foreach (var key in new[] { "diffuseTexture", "obj.mtl.map_Kd", "voxel.textureReference" })
            {
                if (mat.CustomProperties.TryGetValue(key, out var prop) &&
                    prop.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(prop.GetString()))
                {
                    return true;
                }
            }
        }

        // 2. Resolve directory to search on disk
        string? baseDirectory = null;
        if (!string.IsNullOrWhiteSpace(explicitFilePath))
        {
            try
            {
                baseDirectory = File.Exists(explicitFilePath)
                    ? Path.GetDirectoryName(explicitFilePath)
                    : (Directory.Exists(explicitFilePath) ? explicitFilePath : null);
            }
            catch { }
        }

        if (string.IsNullOrEmpty(baseDirectory))
        {
            var src = scene.Metadata.Source?.OriginalFileName;
            if (!string.IsNullOrWhiteSpace(src))
            {
                try
                {
                    baseDirectory = File.Exists(src)
                        ? Path.GetDirectoryName(src)
                        : (Directory.Exists(src) ? src : null);
                }
                catch { }
            }
        }

        if (!string.IsNullOrEmpty(baseDirectory) && Directory.Exists(baseDirectory))
        {
            var searchDirs = new List<string>
            {
                baseDirectory,
                Path.Combine(baseDirectory, "textures", "entity"),
                Path.Combine(baseDirectory, "textures"),
                Path.Combine(baseDirectory, "textures", "models"),
                Path.Combine(baseDirectory, "texture")
            };

            var parent = Directory.GetParent(baseDirectory)?.FullName;
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
            {
                searchDirs.Add(Path.Combine(parent, "textures", "entity"));
                searchDirs.Add(Path.Combine(parent, "textures"));
                searchDirs.Add(parent);
            }

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "skin", "steve", "alex", "default", "texture"
            };

            if (document != null)
            {
                foreach (var g in document.Geometries)
                {
                    string id = g.Description.Identifier;
                    if (!string.IsNullOrEmpty(id))
                    {
                        candidates.Add(id);
                        if (id.StartsWith("geometry.", StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(id[9..]);
                        }
                    }
                }
            }

            foreach (var mat in scene.Materials.Materials)
            {
                string name = mat.Name;
                if (!string.IsNullOrEmpty(name))
                {
                    candidates.Add(name);
                    if (name.StartsWith("geometry.", StringComparison.OrdinalIgnoreCase))
                    {
                        name = name[9..];
                    }
                    if (name.EndsWith("_material", StringComparison.OrdinalIgnoreCase))
                    {
                        name = name[..^9];
                    }
                    candidates.Add(name);
                }
            }

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var cand in candidates)
                {
                    string p = Path.Combine(dir, $"{cand}.png");
                    if (File.Exists(p)) return true;
                }

                try
                {
                    var pngs = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
                    if (pngs.Length == 1) return true;
                }
                catch { }
            }
        }

        return false;
    }

    /// <summary>
    /// Recalculates the underlying SceneNode's Mesh vertices using <see cref="BedrockCoordinateConverter"/>
    /// and notifies Viewport to refresh immediately.
    /// </summary>
    public void RecalculateCubeMesh(BedrockCubeItemViewModel cubeVm)
    {
        ArgumentNullException.ThrowIfNull(cubeVm);
        if (_currentScene is null || !cubeVm.NodeId.HasValue) return;

        var cubeNode = _currentScene.Graph.FindNode(cubeVm.NodeId.Value);
        if (cubeNode?.Object is null) return;

        Vector3D origin = ParseVector3D(cubeVm.Origin, Vector3D.Zero);
        Vector3D size = ParseVector3D(cubeVm.Size, Vector3D.One);
        Vector3D pivot = ParseVector3D(cubeVm.Pivot, Vector3D.Zero);
        Vector3D rot = ParseVector3D(cubeVm.Rotation, Vector3D.Zero);

        BedrockCubeUv? uv = null;
        if (!string.IsNullOrWhiteSpace(cubeVm.Uv))
        {
            if (cubeVm.Uv.Contains("PerFace", StringComparison.OrdinalIgnoreCase))
            {
                uv = cubeVm.SourceCube?.Uv ?? BedrockCubeUv.CreatePerFaceUv();
            }
            else
            {
                Vector2D uvOffset = ParseVector2D(cubeVm.Uv, Vector2D.Zero);
                uv = BedrockCubeUv.CreateBoxUv(uvOffset.X, uvOffset.Y);
            }
        }

        BedrockCube bedrockCube;
        if (cubeVm.SourceCube is not null &&
            string.Equals(cubeVm.Origin, cubeVm.OriginalOrigin, StringComparison.Ordinal) &&
            string.Equals(cubeVm.Size, cubeVm.OriginalSize, StringComparison.Ordinal) &&
            string.Equals(cubeVm.Pivot, cubeVm.OriginalPivot, StringComparison.Ordinal) &&
            string.Equals(cubeVm.Rotation, cubeVm.OriginalRotation, StringComparison.Ordinal) &&
            string.Equals(cubeVm.Uv, cubeVm.OriginalUv, StringComparison.Ordinal) &&
            Math.Abs(cubeVm.Inflate - cubeVm.OriginalInflate) < 1e-6 &&
            cubeVm.Mirror == cubeVm.OriginalMirror)
        {
            bedrockCube = cubeVm.SourceCube;
        }
        else
        {
            bedrockCube = new BedrockCube
            {
                Origin = origin,
                Size = size,
                Pivot = pivot,
                Rotation = rot != Vector3D.Zero ? rot : null,
                Inflate = cubeVm.Inflate,
                Mirror = cubeVm.Mirror,
                Uv = uv
            };
        }

        BedrockBone owningBone = cubeVm.SourceBone ?? new BedrockBone
        {
            Name = cubeVm.BoneName,
            Pivot = ParseVector3D(SelectedBone?.Pivot, Vector3D.Zero)
        };

        var desc = cubeVm.SourceDescription ?? new BedrockGeometryDescription
        {
            Identifier = cubeVm.GeometryId,
            TextureWidth = 64,
            TextureHeight = 64
        };

        Guid? matId = cubeVm.MaterialId;
        if (!matId.HasValue && cubeNode.Object.Meshes.Count > 0 && cubeNode.Object.Meshes[0].Faces.Count > 0)
        {
            matId = cubeNode.Object.Meshes[0].Faces[0].MaterialId;
        }

        MeshData newMesh = BedrockCoordinateConverter.BuildCubeMesh(
            bedrockCube,
            owningBone,
            desc,
            cubeNode.Name,
            matId);

        if (cubeNode.Object.Meshes.Count > 0)
        {
            var existingMesh = cubeNode.Object.Meshes[0];
            existingMesh.Vertices.Clear();
            existingMesh.Vertices.AddRange(newMesh.Vertices);
            existingMesh.Faces.Clear();
            existingMesh.Faces.AddRange(newMesh.Faces);
        }
        else
        {
            cubeNode.Object.Meshes.Add(newMesh);
        }

        // Synchronize node object properties
        cubeNode.Object.Properties["Origin"] = JsonSerializer.SerializeToElement(new[] { origin.X, origin.Y, origin.Z });
        cubeNode.Object.Properties["Size"] = JsonSerializer.SerializeToElement(new[] { size.X, size.Y, size.Z });
        cubeNode.Object.Properties["Pivot"] = JsonSerializer.SerializeToElement(new[] { pivot.X, pivot.Y, pivot.Z });
        cubeNode.Object.Properties["Rotation"] = JsonSerializer.SerializeToElement(new[] { rot.X, rot.Y, rot.Z });
        cubeNode.Object.Properties["UV"] = JsonSerializer.SerializeToElement(cubeVm.Uv);
        cubeNode.Object.Properties["Inflate"] = JsonSerializer.SerializeToElement(cubeVm.Inflate);
        cubeNode.Object.Properties["Mirror"] = JsonSerializer.SerializeToElement(cubeVm.Mirror);

        cubeNode.Object.Properties["bedrock.origin"] = cubeNode.Object.Properties["Origin"];
        cubeNode.Object.Properties["bedrock.size"] = cubeNode.Object.Properties["Size"];
        cubeNode.Object.Properties["bedrock.pivot"] = cubeNode.Object.Properties["Pivot"];
        cubeNode.Object.Properties["bedrock.rotation"] = cubeNode.Object.Properties["Rotation"];
        cubeNode.Object.Properties["bedrock.uv"] = cubeNode.Object.Properties["UV"];
        cubeNode.Object.Properties["bedrock.inflate"] = cubeNode.Object.Properties["Inflate"];
        cubeNode.Object.Properties["bedrock.mirror"] = cubeNode.Object.Properties["Mirror"];

        if (_inspector is not null)
        {
            _inspector.InspectUsmNode(cubeNode, _currentScene);
        }

        CubeMeshUpdated?.Invoke(this, cubeVm);
        ViewportRefresher?.Invoke();
    }

    [RelayCommand]
    public void ResetTransform()
    {
        SelectedCube?.ResetTransform();
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

        // Ensure SelectedBone reflects the cube's owning bone
        if (!string.IsNullOrEmpty(cube.BoneName) && (SelectedBone is null || SelectedBone.Name != cube.BoneName))
        {
            var owningBone = GetAllBones(RootBones).FirstOrDefault(b => b.Name == cube.BoneName);
            if (owningBone is not null)
            {
                SelectedBone = owningBone;
            }
        }

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

        if (SelectedCube is not null &&
            (string.Equals(SelectedCube.NodeId?.ToString(), nodeId, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(SelectedCube.CubeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(SelectedCube.Name, nodeId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var candidateBones = Geometries.Count > 0
            ? Geometries.SelectMany(g => GetAllBones(g.Bones))
            : GetAllBones(RootBones);

        foreach (var bone in candidateBones)
        {
            var matchingCube = bone.Cubes.FirstOrDefault(c =>
                string.Equals(c.NodeId?.ToString(), nodeId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.CubeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Name, nodeId, StringComparison.OrdinalIgnoreCase));

            if (matchingCube is not null)
            {
                SelectedGeometry = Geometries.FirstOrDefault(g => g.Bones.Any(b => b.Name == bone.Name || GetAllBones(b.Children).Any(c => c.Name == bone.Name)))
                                   ?? Geometries.FirstOrDefault();
                SelectedBone = bone;
                SelectedCube = matchingCube;

                if (SelectedGeometry is not null) SelectedGeometry.IsExpanded = true;
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
                SelectedGeometry = Geometries.FirstOrDefault(g => g.Bones.Any(b => b.Name == bone.Name || GetAllBones(b.Children).Any(c => c.Name == bone.Name)))
                                   ?? Geometries.FirstOrDefault();
                SelectedBone = bone;
                if (SelectedGeometry is not null) SelectedGeometry.IsExpanded = true;
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

    public static Vector3D ParseVector3D(string? input, Vector3D fallback = default)
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        var cleaned = input.Replace("°", "").Replace("[", "").Replace("]", "");
        var parts = cleaned.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
        {
            return new Vector3D(x, y, z);
        }
        return fallback;
    }

    public static Vector2D ParseVector2D(string? input, Vector2D fallback = default)
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        var cleaned = input.Replace("[", "").Replace("]", "");
        var parts = cleaned.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
        {
            return new Vector2D(x, y);
        }
        return fallback;
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

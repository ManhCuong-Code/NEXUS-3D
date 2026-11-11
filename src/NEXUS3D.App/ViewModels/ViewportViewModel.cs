using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Profiles;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Primary Workspace ViewModel coordinating the 3D Viewport, Multi-Tab documents,
/// Camera, Specialized Workspaces (Mesh, Block, Voxel, Bedrock), and Conversion Assistant.
/// </summary>
public sealed partial class ViewportViewModel : BaseViewModel
{
    private readonly IRenderService _renderService;
    private readonly IWorkspaceSceneSession _workspaceSession;
    private readonly ITransformService _transformService;
    private readonly ISelectionService _selectionService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly IPivotService _pivotService;
    private readonly IFileClassificationService _fileClassificationService;
    private readonly IConversionCompatibilityService _compatibilityService;
    private readonly IConversionExecutor _conversionExecutor;
    private readonly ILogger<ViewportViewModel> _logger;

    public CameraViewModel Camera { get; }
    public RenderSettingsViewModel Settings { get; }
    public SceneHierarchyViewModel Hierarchy { get; }
    public ObjectInspectorViewModel Inspector { get; }
    public BoneHierarchyViewModel Skeletons { get; }

    public BlockWorkspaceViewModel BlockWorkspace { get; }
    public VoxelWorkspaceViewModel VoxelWorkspace { get; }
    public BedrockWorkspaceViewModel BedrockWorkspace { get; }

    public ObservableCollection<WorkspaceDocumentTab> Tabs { get; } = [];

    [ObservableProperty]
    private WorkspaceDocumentTab? _activeTab;

    [ObservableProperty]
    private RenderScene? _scene;

    [ObservableProperty]
    private Scene3D? _rawUsmScene;

    [ObservableProperty]
    private string _loadedModelName = "No Model Loaded";

    [ObservableProperty]
    private string _loadedFilePath = string.Empty;

    // --- Active Editor State ---
    [ObservableProperty]
    private EditorToolType _activeTool = EditorToolType.Select;

    [ObservableProperty]
    private TransformSpace _activeSpace = TransformSpace.World;

    [ObservableProperty]
    private bool _snappingEnabled;

    [ObservableProperty]
    private string? _selectedNodeId;

    public ITransformService TransformService => _transformService;
    public ISelectionService SelectionService => _selectionService;
    public IUndoRedoService UndoRedoService => _undoRedoService;
    public IPivotService PivotService => _pivotService;

    public ViewportViewModel(
        IRenderService renderService,
        IWorkspaceSceneSession workspaceSession,
        ITransformService transformService,
        ISelectionService selectionService,
        IUndoRedoService undoRedoService,
        IPivotService pivotService,
        IFileClassificationService fileClassificationService,
        IConversionCompatibilityService compatibilityService,
        IConversionExecutor conversionExecutor,
        CameraViewModel camera,
        RenderSettingsViewModel settings,
        SceneHierarchyViewModel hierarchy,
        ObjectInspectorViewModel inspector,
        BoneHierarchyViewModel skeletons,
        BlockWorkspaceViewModel blockWorkspace,
        VoxelWorkspaceViewModel voxelWorkspace,
        BedrockWorkspaceViewModel bedrockWorkspace,
        ILogger<ViewportViewModel> logger)
    {
        _renderService = renderService;
        _workspaceSession = workspaceSession;
        _transformService = transformService;
        _selectionService = selectionService;
        _undoRedoService = undoRedoService;
        _pivotService = pivotService;
        _fileClassificationService = fileClassificationService;
        _compatibilityService = compatibilityService;
        _conversionExecutor = conversionExecutor;

        Camera = camera;
        Settings = settings;
        Hierarchy = hierarchy;
        Inspector = inspector;
        Skeletons = skeletons;

        BlockWorkspace = blockWorkspace;
        VoxelWorkspace = voxelWorkspace;
        BedrockWorkspace = bedrockWorkspace;

        _logger = logger;

        Title = "3D Viewport Workspace";
        Subtitle = "Universal real-time rendering, multi-format inspection, and interactive 3D editing engine.";

        Hierarchy.NodeSelected += OnHierarchyNodeSelected;
        Skeletons.BoneSelected += OnSkeletonBoneSelected;
        Settings.SettingsChanged += OnSettingsChanged;

        _selectionService.SelectionChanged += OnSelectionServiceChanged;
        _workspaceSession.SceneChanged += OnWorkspaceSessionSceneChanged;
    }

    partial void OnActiveTabChanged(WorkspaceDocumentTab? oldValue, WorkspaceDocumentTab? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.CameraState = Camera.CurrentCamera;
            oldValue.Settings = Settings.CurrentSettings;
        }

        if (newValue is null)
        {
            RawUsmScene = null;
            Scene = null;
            LoadedModelName = "No Model Loaded";
            LoadedFilePath = string.Empty;
            Hierarchy.RootNodes.Clear();
            Skeletons.RootBones.Clear();
            _selectionService.ClearSelection();
            Inspector.Clear();
            return;
        }

        RawUsmScene = newValue.RawUsmScene;
        Scene = newValue.RenderScene;
        LoadedModelName = newValue.Title;
        LoadedFilePath = newValue.FilePath;

        if (newValue.CameraState != default)
        {
            Camera.CurrentCamera = newValue.CameraState;
        }

        if (newValue.RenderScene is not null)
        {
            Hierarchy.LoadScene(newValue.RenderScene);
            Skeletons.LoadSkeletons(newValue.RenderScene.Skeletons);
        }

        // Activate specialized workspace based on document category
        if (newValue.RawUsmScene is not null)
        {
            switch (newValue.Category)
            {
                case FormatCategory.MinecraftSchematic:
                    BlockWorkspace.LoadFromScene(newValue.RawUsmScene);
                    break;
                case FormatCategory.VoxelModel:
                    VoxelWorkspace.LoadFromScene(newValue.RawUsmScene);
                    break;
                case FormatCategory.BedrockGeometry:
                    BedrockWorkspace.LoadFromScene(newValue.RawUsmScene);
                    break;
            }

            _workspaceSession.SetActiveScene(newValue.RawUsmScene, newValue.FilePath, newValue.FormatDescriptor);
        }

        _selectionService.ClearSelection();
        Inspector.Clear();
    }

    private async void OnWorkspaceSessionSceneChanged(object? sender, SceneSessionChangedEventArgs e)
    {
        if (e.Scene is not null && !ReferenceEquals(e.Scene, RawUsmScene))
        {
            await LoadSceneModelAsync(e.Scene, e.FilePath);
        }
        else if (e.Scene is null && Tabs.Count == 0)
        {
            RawUsmScene = null;
            Scene = null;
            LoadedModelName = "No Model Loaded";
            LoadedFilePath = string.Empty;
            Hierarchy.RootNodes.Clear();
            Skeletons.RootBones.Clear();
            _selectionService.ClearSelection();
            Inspector.Clear();
        }
    }

    public async Task LoadSceneModelAsync(Scene3D sceneModel, string? sourcePath = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sceneModel);

        try
        {
            IsBusy = true;
            StatusMessage = $"Preparing 3D render scene for '{sceneModel.Name}'...";

            RawUsmScene = sceneModel;
            var rScene = await _renderService.LoadSceneAsync(sceneModel, cancellationToken);
            Scene = rScene;
            LoadedModelName = sceneModel.Name;
            LoadedFilePath = sourcePath ?? string.Empty;

            Hierarchy.LoadScene(rScene);
            Skeletons.LoadSkeletons(rScene.Skeletons);
            _selectionService.ClearSelection();
            Inspector.Clear();

            // Resolve or update tab
            var existingTab = Tabs.FirstOrDefault(t => !string.IsNullOrEmpty(sourcePath) && string.Equals(t.FilePath, sourcePath, StringComparison.OrdinalIgnoreCase));
            if (existingTab is null)
            {
                var category = ResolveCategoryForScene(sceneModel, sourcePath);
                var profileType = category switch
                {
                    FormatCategory.PolygonMesh => ViewerProfileType.Mesh,
                    FormatCategory.MinecraftSchematic => ViewerProfileType.Block,
                    FormatCategory.VoxelModel => ViewerProfileType.Voxel,
                    FormatCategory.BedrockGeometry => ViewerProfileType.BedrockGeometry,
                    _ => ViewerProfileType.Generic
                };
                var profile = ViewerProfileRegistry.GetProfile(profileType);
                var tab = new WorkspaceDocumentTab
                {
                    Title = string.IsNullOrWhiteSpace(sceneModel.Name) ? (Path.GetFileName(sourcePath) ?? "Model") : sceneModel.Name,
                    FilePath = sourcePath ?? string.Empty,
                    Category = category,
                    ViewerProfile = profile,
                    RawUsmScene = sceneModel,
                    RenderScene = rScene,
                    CameraState = Camera.CurrentCamera,
                    Settings = Settings.CurrentSettings
                };
                tab.UpdateStats(rScene);
                Tabs.Add(tab);
                ActiveTab = tab;
            }
            else
            {
                existingTab.RawUsmScene = sceneModel;
                existingTab.RenderScene = rScene;
                existingTab.UpdateStats(rScene);
                ActiveTab = existingTab;
            }

            FocusModel();

            StatusMessage = $"Loaded '{sceneModel.Name}' ({rScene.TotalTriangles:N0} triangles, {rScene.TotalVoxels:N0} voxels).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scene model into viewport");
            ErrorMessage = $"Error rendering scene: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            ErrorMessage = "Specified model file does not exist.";
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            StatusMessage = $"Reading model '{Path.GetFileName(filePath)}'...";

            var loadResult = await _workspaceSession.LoadFileIntoSessionAsync(filePath, cancellationToken);
            if (loadResult.IsFailure || loadResult.Value is null)
            {
                ErrorMessage = loadResult.ErrorMessage ?? "Failed to parse model.";
                return;
            }

            await LoadSceneModelAsync(loadResult.Value, filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading model file {Path}", filePath);
            ErrorMessage = $"Failed to open model: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InspectAndOpenFileAsync(string filePath, bool showInspectionDialog = true)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        var report = _fileClassificationService.ClassifyFile(filePath);

        if (showInspectionDialog)
        {
            var inspectionVm = new FileOpenInspectionViewModel(report);
            var window = new Views.FileOpenInspectionWindow(inspectionVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true) return;
        }

        await LoadFileAsync(filePath);
    }

    [RelayCommand]
    public async Task OpenNewTabAsync()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Open 3D Model in New Tab",
            Filter = "All Supported 3D Models (*.obj;*.schem;*.schematic;*.voxel.json;*.geo.json)|*.obj;*.schem;*.schematic;*.voxel.json;*.geo.json|Wavefront OBJ (*.obj)|*.obj|Minecraft Schematic (*.schem;*.schematic)|*.schem;*.schematic|Voxel JSON (*.voxel.json)|*.voxel.json|Bedrock Geometry (*.geo.json)|*.geo.json|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            await InspectAndOpenFileAsync(ofd.FileName, showInspectionDialog: true);
        }
    }

    [RelayCommand]
    public void SelectTab(WorkspaceDocumentTab? tab)
    {
        if (tab is not null)
        {
            ActiveTab = tab;
        }
    }

    [RelayCommand]
    public void CloseTab(WorkspaceDocumentTab? tab)
    {
        if (tab is null) return;

        int index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (ActiveTab == tab)
        {
            if (Tabs.Count > 0)
            {
                int newIndex = Math.Clamp(index, 0, Tabs.Count - 1);
                ActiveTab = Tabs[newIndex];
            }
            else
            {
                ActiveTab = null;
            }
        }
    }

    [RelayCommand]
    public void ConvertActiveModel()
    {
        if (RawUsmScene is null)
        {
            ErrorMessage = "Please load a 3D model first before opening the Conversion Assistant.";
            return;
        }

        var sourceFormat = ActiveTab?.FormatType ?? ModelFormatType.Unknown;
        if (sourceFormat == ModelFormatType.Unknown && !string.IsNullOrEmpty(LoadedFilePath))
        {
            var rep = _fileClassificationService.ClassifyFile(LoadedFilePath);
            sourceFormat = rep.FormatType;
        }

        var convertVm = new ConvertModelDialogViewModel(
            RawUsmScene,
            LoadedFilePath,
            sourceFormat,
            _compatibilityService,
            _conversionExecutor);

        convertVm.OpenConvertedFileRequested += async (s, path) =>
        {
            await InspectAndOpenFileAsync(path, showInspectionDialog: false);
        };

        var window = new Views.ConvertModelWindow(convertVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
    }

    private FormatCategory ResolveCategoryForScene(Scene3D scene, string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            var report = _fileClassificationService.ClassifyFile(path);
            if (report.Category != FormatCategory.Unknown) return report.Category;
        }

        bool hasVoxels = scene.Graph.Nodes.Any(n => n.Object?.VoxelGrids.Count > 0);
        bool hasBones = scene.Skeletons.Count > 0 || scene.Graph.Nodes.Any(n => n.Object?.SkeletonId is not null);
        bool hasMeshes = scene.Graph.Nodes.Any(n => n.Object?.Meshes.Count > 0);

        if (hasBones) return FormatCategory.BedrockGeometry;
        if (hasVoxels) return FormatCategory.MinecraftSchematic;
        if (hasMeshes) return FormatCategory.PolygonMesh;

        return FormatCategory.Unknown;
    }

    [RelayCommand]
    public void SelectNode(string? nodeId)
    {
        SelectedNodeId = nodeId;
        _renderService.SelectNode(nodeId);
        Hierarchy.SelectById(nodeId);

        if (!string.IsNullOrEmpty(nodeId))
        {
            if (Guid.TryParse(nodeId, out var guid))
            {
                _selectionService.Select(guid);
                var usmNode = RawUsmScene?.Graph.FindNode(guid);
                if (usmNode is not null)
                {
                    Inspector.InspectUsmNode(usmNode, RawUsmScene);
                    return;
                }
            }

            if (Scene is not null)
            {
                var rNode = FindNodeRecursive(Scene.RootNodes, nodeId);
                Inspector.InspectNode(rNode, Scene);
            }
        }
        else
        {
            _selectionService.ClearSelection();
            Inspector.Clear();
        }
    }

    [RelayCommand]
    public void SetTool(EditorToolType tool)
    {
        ActiveTool = tool;
        _transformService.ActiveTool = tool;
    }

    [RelayCommand]
    public void ToggleSpace()
    {
        ActiveSpace = ActiveSpace == TransformSpace.World ? TransformSpace.Local : TransformSpace.World;
        _transformService.TransformSpace = ActiveSpace;
        Inspector.ActiveSpace = ActiveSpace;
    }

    [RelayCommand]
    public void ToggleSnapping()
    {
        SnappingEnabled = !SnappingEnabled;
        _transformService.SnappingEnabled = SnappingEnabled;
    }

    [RelayCommand]
    public void Undo()
    {
        _undoRedoService.Undo();
        if (Inspector.ActiveNode is not null)
        {
            Inspector.InspectUsmNode(Inspector.ActiveNode, RawUsmScene);
        }
    }

    [RelayCommand]
    public void Redo()
    {
        _undoRedoService.Redo();
        if (Inspector.ActiveNode is not null)
        {
            Inspector.InspectUsmNode(Inspector.ActiveNode, RawUsmScene);
        }
    }

    [RelayCommand]
    public void FocusModel()
    {
        if (Scene is null) return;
        var min = Scene.BoundsMin;
        var max = Scene.BoundsMax;
        var center = (min + max) * 0.5f;
        float maxDim = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
        Camera.Focus(center, Math.Max(maxDim * 0.8f, 2.0f));
    }

    [RelayCommand]
    public async Task MoveModelToFloorAsync()
    {
        if (Scene is null || RawUsmScene is null) return;
        float minY = Scene.BoundsMin.Y;
        if (Math.Abs(minY) < 1e-4) return;

        double deltaY = -minY;
        var p = RawUsmScene.Graph.Root.LocalTransform.Position;
        RawUsmScene.Graph.Root.LocalTransform.Position = new Vector3D(p.X, p.Y + deltaY, p.Z);

        await LoadSceneModelAsync(RawUsmScene, LoadedFilePath);
    }

    [RelayCommand]
    public async Task CenterModelAsync()
    {
        if (Scene is null || RawUsmScene is null) return;
        var center = (Scene.BoundsMin + Scene.BoundsMax) * 0.5f;
        if (Math.Abs(center.X) < 1e-4 && Math.Abs(center.Z) < 1e-4) return;

        double deltaX = -center.X;
        double deltaZ = -center.Z;
        var p = RawUsmScene.Graph.Root.LocalTransform.Position;
        RawUsmScene.Graph.Root.LocalTransform.Position = new Vector3D(p.X + deltaX, p.Y, p.Z + deltaZ);

        await LoadSceneModelAsync(RawUsmScene, LoadedFilePath);
    }

    [RelayCommand]
    public async Task ResetModelPositionAsync()
    {
        if (RawUsmScene is null) return;
        RawUsmScene.Graph.Root.LocalTransform = Transform3D.Identity();

        await LoadSceneModelAsync(RawUsmScene, LoadedFilePath);
    }

    private void OnHierarchyNodeSelected(object? sender, string? nodeId)
    {
        SelectNode(nodeId);
    }

    private void OnSkeletonBoneSelected(object? sender, string? boneName)
    {
        if (string.IsNullOrEmpty(boneName) || Scene is null)
        {
            Inspector.Clear();
            return;
        }

        var bone = FindBoneRecursive(Scene.Skeletons, boneName);
        Inspector.InspectBone(bone);
    }

    private void OnSettingsChanged(object? sender, RenderSettings settings)
    {
        if (ActiveTab is not null)
        {
            ActiveTab.Settings = settings;
        }
    }

    private void OnSelectionServiceChanged(object? sender, EventArgs e)
    {
        var primary = _selectionService.PrimarySelectedNodeId;
        SelectedNodeId = primary?.ToString();
    }

    private static RenderNode? FindNodeRecursive(IEnumerable<RenderNode> nodes, string id)
    {
        foreach (var n in nodes)
        {
            if (string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase)) return n;
            var c = FindNodeRecursive(n.Children, id);
            if (c is not null) return c;
        }
        return null;
    }

    private static RenderBone? FindBoneRecursive(IEnumerable<RenderBone> bones, string name)
    {
        foreach (var b in bones)
        {
            if (string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)) return b;
            var c = FindBoneRecursive(b.Children, name);
            if (c is not null) return c;
        }
        return null;
    }
}

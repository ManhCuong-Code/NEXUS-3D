using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Primary Workspace ViewModel coordinating the 3D Viewport, Camera, RenderSettings, Hierarchy,
/// Object Inspector, and Interactive 3D Editor Tools.
/// </summary>
public sealed partial class ViewportViewModel : BaseViewModel
{
    private readonly IRenderService _renderService;
    private readonly IEnumerable<IFormatParser> _parsers;
    private readonly ITransformService _transformService;
    private readonly ISelectionService _selectionService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly IPivotService _pivotService;
    private readonly ILogger<ViewportViewModel> _logger;

    public CameraViewModel Camera { get; }
    public RenderSettingsViewModel Settings { get; }
    public SceneHierarchyViewModel Hierarchy { get; }
    public ObjectInspectorViewModel Inspector { get; }
    public BoneHierarchyViewModel Skeletons { get; }

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
        IEnumerable<IFormatParser> parsers,
        ITransformService transformService,
        ISelectionService selectionService,
        IUndoRedoService undoRedoService,
        IPivotService pivotService,
        CameraViewModel camera,
        RenderSettingsViewModel settings,
        SceneHierarchyViewModel hierarchy,
        ObjectInspectorViewModel inspector,
        BoneHierarchyViewModel skeletons,
        ILogger<ViewportViewModel> logger)
    {
        _renderService = renderService;
        _parsers = parsers;
        _transformService = transformService;
        _selectionService = selectionService;
        _undoRedoService = undoRedoService;
        _pivotService = pivotService;
        Camera = camera;
        Settings = settings;
        Hierarchy = hierarchy;
        Inspector = inspector;
        Skeletons = skeletons;
        _logger = logger;

        Title = "3D Viewport Workspace";
        Subtitle = "Universal real-time rendering, inspection, and interactive 3D editing engine.";

        Hierarchy.NodeSelected += OnHierarchyNodeSelected;
        Skeletons.BoneSelected += OnSkeletonBoneSelected;
        Settings.SettingsChanged += OnSettingsChanged;

        _selectionService.SelectionChanged += OnSelectionServiceChanged;
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

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            var parser = _parsers.FirstOrDefault(p =>
                (ext is ".schem" or ".schematic" && p.FormatIdentifier.Contains("schematic", StringComparison.OrdinalIgnoreCase)) ||
                (ext is ".obj" && p.FormatIdentifier.Contains("obj", StringComparison.OrdinalIgnoreCase)) ||
                (ext is ".json" or ".voxel.json" && p.FormatIdentifier.Contains("voxel", StringComparison.OrdinalIgnoreCase)) ||
                (ext is ".geo.json" && p.FormatIdentifier.Contains("bedrock", StringComparison.OrdinalIgnoreCase)) ||
                p.FormatIdentifier.Contains(ext.TrimStart('.'), StringComparison.OrdinalIgnoreCase));

            if (parser is null)
            {
                ErrorMessage = $"Unsupported 3D file format: '{ext}'.";
                return;
            }

            using var stream = File.OpenRead(filePath);
            var parseResult = await parser.ParseToSceneAsync(
                stream,
                new ParseOptions { StrictValidation = false, PreserveUnknownData = true },
                cancellationToken);

            if (parseResult.IsFailure || parseResult.Value is null)
            {
                ErrorMessage = $"Failed to parse model: {parseResult.Error.Message}";
                return;
            }

            await LoadSceneModelAsync(parseResult.Value, filePath, cancellationToken);
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
        // Viewport control automatically observes settings changes
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

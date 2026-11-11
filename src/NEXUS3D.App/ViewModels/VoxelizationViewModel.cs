using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;
using NEXUS3D.Voxelization.Domain.Models;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Workspace ViewModel managing interactive 3D voxelization, algorithm configuration,
/// real-time viewport preview, greedy cuboid merging, and telemetry diagnostics.
/// </summary>
public sealed partial class VoxelizationViewModel : BaseViewModel
{
    private readonly IVoxelizationEngine _voxelizationEngine;
    private readonly IVoxelToCuboidConverter _cuboidConverter;
    private readonly IRenderSceneAdapter _renderSceneAdapter;
    private readonly IFormatRegistry _formatRegistry;
    private readonly IWorkspaceSceneSession _workspaceSession;
    private readonly INotificationService _notificationService;
    private readonly ILogger<VoxelizationViewModel> _logger;

    private CancellationTokenSource? _cancellationTokenSource;
    private Scene3D? _loadedScene;
    private VoxelGrid? _currentVoxelGrid;

    [ObservableProperty]
    private string _inputFilePath = string.Empty;

    [ObservableProperty]
    private string _meshSummary = "No 3D mesh loaded. Please browse or select a model.";

    [ObservableProperty]
    private VoxelizationMode _selectedMode = VoxelizationMode.SurfaceOnly;

    [ObservableProperty]
    private VoxelResolutionMode _selectedResolutionMode = VoxelResolutionMode.ByMaxGridDimension;

    [ObservableProperty]
    private double _voxelSize = 1.0;

    [ObservableProperty]
    private int _maxGridDimension = 64;

    [ObservableProperty]
    private ColorSamplingMode _selectedColorSampling = ColorSamplingMode.MaterialDominance;

    [ObservableProperty]
    private VoxelGridAlignment _selectedAlignment = VoxelGridAlignment.CenterOnOrigin;

    [ObservableProperty]
    private bool _removeIsolatedVoxels = false;

    [ObservableProperty]
    private bool _fillInternalGaps = true;

    [ObservableProperty]
    private bool _mergeAdjacentVoxels = true;

    [ObservableProperty]
    private bool _preserveMaterials = true;

    [ObservableProperty]
    private string _defaultBlockNamespace = "minecraft:stone";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _progressStatus = "Ready";

    [ObservableProperty]
    private bool _canCancel;

    [ObservableProperty]
    private RenderScene? _viewportScene;

    [ObservableProperty]
    private int _resultVoxelCount;

    [ObservableProperty]
    private int _resultCuboidCount;

    [ObservableProperty]
    private string _reductionRatioText = "N/A";

    [ObservableProperty]
    private string _processingTimeText = "0 ms";

    [ObservableProperty]
    private string _statsSummaryText = "Voxelization has not been run yet.";

    [ObservableProperty]
    private bool _hasResult;

    public ObservableCollection<string> Warnings { get; } = [];

    public IReadOnlyList<VoxelizationMode> AvailableModes { get; } =
    [
        VoxelizationMode.SurfaceOnly,
        VoxelizationMode.SolidVolume,
        VoxelizationMode.Conservative,
        VoxelizationMode.Adaptive
    ];

    public IReadOnlyList<VoxelResolutionMode> AvailableResolutionModes { get; } =
    [
        VoxelResolutionMode.ByMaxGridDimension,
        VoxelResolutionMode.ByVoxelSize
    ];

    public IReadOnlyList<ColorSamplingMode> AvailableColorSamplings { get; } =
    [
        ColorSamplingMode.MaterialDominance,
        ColorSamplingMode.TextureSampling,
        ColorSamplingMode.VertexColorInterpolation,
        ColorSamplingMode.UniformFallback
    ];

    public IReadOnlyList<VoxelGridAlignment> AvailableAlignments { get; } =
    [
        VoxelGridAlignment.CenterOnOrigin,
        VoxelGridAlignment.MinAtOrigin,
        VoxelGridAlignment.PreserveWorldCoordinates
    ];

    public VoxelizationViewModel(
        IVoxelizationEngine voxelizationEngine,
        IVoxelToCuboidConverter cuboidConverter,
        IRenderSceneAdapter renderSceneAdapter,
        IFormatRegistry formatRegistry,
        IWorkspaceSceneSession workspaceSession,
        INotificationService notificationService,
        ILogger<VoxelizationViewModel> logger)
    {
        _voxelizationEngine = Guard.NotNull(voxelizationEngine);
        _cuboidConverter = Guard.NotNull(cuboidConverter);
        _renderSceneAdapter = Guard.NotNull(renderSceneAdapter);
        _formatRegistry = Guard.NotNull(formatRegistry);
        _workspaceSession = Guard.NotNull(workspaceSession);
        _notificationService = Guard.NotNull(notificationService);
        _logger = Guard.NotNull(logger);

        Title = "Voxelization Engine Workspace";
        Subtitle = "Universal polygon mesh to discrete voxel converter with SAT rasterization & greedy cuboid optimization.";

        _workspaceSession.SceneChanged += OnWorkspaceSessionSceneChanged;
    }

    private void OnWorkspaceSessionSceneChanged(object? sender, SceneSessionChangedEventArgs e)
    {
        if (e.Scene is not null)
        {
            SetLoadedScene(e.Scene, e.FilePath);
        }
        else
        {
            _loadedScene = null;
            InputFilePath = string.Empty;
            MeshSummary = "No 3D mesh loaded. Please browse or select a model.";
            ViewportScene = null;
            _currentVoxelGrid = null;
            HasResult = false;
            ResultVoxelCount = 0;
            ResultCuboidCount = 0;
        }
    }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken = default)
    {
        if (_workspaceSession.ActiveScene is not null && !ReferenceEquals(_loadedScene, _workspaceSession.ActiveScene))
        {
            SetLoadedScene(_workspaceSession.ActiveScene, _workspaceSession.ActiveFilePath);
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseMeshFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select 3D Mesh to Voxelize",
            Filter = _formatRegistry.BuildFileDialogFilter(includeAllSupported: true),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadModelFileAsync(dialog.FileName);
        }
    }

    public async Task LoadModelFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        InputFilePath = filePath;
        IsBusy = true;
        ProgressStatus = $"Loading {Path.GetFileName(filePath)}...";

        try
        {
            var loadResult = await _workspaceSession.LoadFileIntoSessionAsync(filePath);
            if (loadResult.IsFailure || loadResult.Value is null)
            {
                MeshSummary = $"Failed to parse file: {loadResult.ErrorMessage}";
                _notificationService.Publish("Voxelization", MeshSummary, NotificationSeverity.Error, "Voxelization");
                return;
            }

            SetLoadedScene(loadResult.Value, filePath);
            _notificationService.Publish("Voxelization", $"Loaded model '{Path.GetFileName(filePath)}' successfully.", NotificationSeverity.Info, "Voxelization");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading model file for voxelization: {Path}", filePath);
            MeshSummary = $"Error: {ex.Message}";
            _notificationService.Publish("Voxelization", $"Failed to load model: {ex.Message}", NotificationSeverity.Error, "Voxelization");
        }
        finally
        {
            IsBusy = false;
            ProgressStatus = "Ready";
        }
    }

    public void SetLoadedScene(Scene3D scene, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(scene);

        _loadedScene = scene;
        InputFilePath = filePath ?? string.Empty;

        int vertexCount = 0;
        int faceCount = 0;
        int voxelCount = 0;
        foreach (var node in scene.Graph.Nodes)
        {
            if (node.Object is { } obj)
            {
                foreach (var m in obj.Meshes)
                {
                    vertexCount += m.Vertices.Count;
                    faceCount += m.Faces.Count;
                }
                foreach (var g in obj.VoxelGrids)
                {
                    voxelCount += g.GetVoxelCount();
                }
            }
        }

        string name = !string.IsNullOrWhiteSpace(filePath) ? Path.GetFileName(filePath) : scene.Name;
        if (vertexCount > 0)
        {
            MeshSummary = $"{name} • {vertexCount:N0} vertices • {faceCount:N0} faces";
        }
        else if (voxelCount > 0)
        {
            MeshSummary = $"{name} • {voxelCount:N0} source voxels (Voxel Grid Ready)";
        }
        else
        {
            MeshSummary = $"{name} • Scene ready for voxelization";
        }

        ViewportScene = _renderSceneAdapter.Adapt(scene, RenderSettings.Default);
        ProgressStatus = "Source model loaded.";
    }

    [RelayCommand]
    private async Task VoxelizeAsync()
    {
        if (_loadedScene is null)
        {
            _notificationService.Publish("Voxelization", "Please load a 3D model first.", NotificationSeverity.Warning, "Voxelization");
            return;
        }

        IsBusy = true;
        CanCancel = true;
        ProgressPercentage = 0;
        ProgressStatus = "Initializing voxelization engine...";
        Warnings.Clear();

        _cancellationTokenSource = new CancellationTokenSource();
        var ct = _cancellationTokenSource.Token;

        var progress = new Progress<VoxelizationProgress>(p =>
        {
            ProgressPercentage = p.ProgressPercentage;
            ProgressStatus = $"{p.Message} ({p.ProgressPercentage:F0}%)";
        });

        var settings = new VoxelizationSettings
        {
            Mode = SelectedMode,
            ResolutionMode = SelectedResolutionMode,
            VoxelSize = VoxelSize > 0.0001 ? VoxelSize : 1.0,
            MaxGridDimension = Math.Clamp(MaxGridDimension, 8, 1024),
            ColorSampling = SelectedColorSampling,
            GridAlignment = SelectedAlignment,
            RemoveIsolatedVoxels = RemoveIsolatedVoxels,
            FillInternalGaps = FillInternalGaps,
            MergeAdjacentVoxels = MergeAdjacentVoxels,
            PreserveMaterials = PreserveMaterials,
            DefaultBlockNamespace = string.IsNullOrWhiteSpace(DefaultBlockNamespace) ? "minecraft:stone" : DefaultBlockNamespace.Trim()
        };

        try
        {
            var result = await _voxelizationEngine.VoxelizeAsync(_loadedScene, settings, progress, ct);

            if (!result.IsSuccess || result.VoxelGrid is null)
            {
                ProgressStatus = "Voxelization failed.";
                _notificationService.Publish("Voxelization Failed", result.ErrorMessage ?? "Unknown voxelization error.", NotificationSeverity.Error, "Voxelization");
                return;
            }

            _currentVoxelGrid = result.VoxelGrid;
            ResultVoxelCount = result.VoxelGrid.GetVoxelCount();

            // Populate warnings
            foreach (var w in result.Warnings)
            {
                Warnings.Add($"[{w.Severity}] {w.Message}");
            }

            // Populate cuboids
            if (result.Cuboids is not null)
            {
                ResultCuboidCount = result.Cuboids.Count;
                double reduction = ResultVoxelCount > 0
                    ? (1.0 - (double)ResultCuboidCount / ResultVoxelCount) * 100.0
                    : 0.0;
                ReductionRatioText = $"{reduction:F1}% (From {ResultVoxelCount:N0} voxels to {ResultCuboidCount:N0} cuboids)";
            }
            else
            {
                ResultCuboidCount = 0;
                ReductionRatioText = "N/A";
            }

            ProcessingTimeText = $"{result.Statistics.ProcessingTimeMs:F1} ms";
            StatsSummaryText = $"Grid: {result.Statistics.GridDimensionX}×{result.Statistics.GridDimensionY}×{result.Statistics.GridDimensionZ} | " +
                               $"Triangles: {result.Statistics.TotalTrianglesProcessed:N0} | " +
                               $"Rasterized: {result.Statistics.InitialVoxelCount:N0} voxels | " +
                               $"Time: {ProcessingTimeText}";

            // Build preview scene containing the VoxelGrid
            var voxelScene = new Scene3D { Name = "VoxelizedResultScene" };
            var resultNode = new SceneNode("VoxelRoot")
            {
                Object = new SceneObject("VoxelObject")
                {
                    VoxelGrids = { result.VoxelGrid }
                }
            };
            voxelScene.Graph = new SceneGraph(resultNode);

            // Re-render in Viewport
            ViewportScene = _renderSceneAdapter.Adapt(voxelScene, RenderSettings.Default);
            HasResult = true;

            _notificationService.Publish(
                "Voxelization Complete",
                $"Successfully voxelized into {ResultVoxelCount:N0} voxels in {ProcessingTimeText}.",
                NotificationSeverity.Success,
                "Voxelization");
        }
        catch (OperationCanceledException)
        {
            ProgressStatus = "Voxelization cancelled by user.";
            _notificationService.Publish("Voxelization", "Operation was cancelled.", NotificationSeverity.Warning, "Voxelization");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during voxelization.");
            ProgressStatus = $"Error: {ex.Message}";
            _notificationService.Publish("Voxelization Error", ex.Message, NotificationSeverity.Error, "Voxelization");
        }
        finally
        {
            IsBusy = false;
            CanCancel = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (CanCancel && _cancellationTokenSource is not null)
        {
            _cancellationTokenSource.Cancel();
            ProgressStatus = "Cancelling voxelization...";
        }
    }

    [RelayCommand]
    private async Task OptimizeCuboidsAsync()
    {
        if (_currentVoxelGrid is null)
        {
            _notificationService.Publish("Greedy Optimization", "No voxel grid available to optimize.", NotificationSeverity.Warning, "Voxelization");
            return;
        }

        try
        {
            var optResult = await _cuboidConverter.ConvertToCuboidsAsync(_currentVoxelGrid);
            ResultCuboidCount = optResult.Cuboids.Count;
            ReductionRatioText = $"{optResult.ReductionPercentage:F1}% (From {optResult.OriginalVoxelCount:N0} voxels to {ResultCuboidCount:N0} cuboids in {optResult.ElapsedMilliseconds:F1}ms)";
            _notificationService.Publish("Greedy Cuboid Optimization", $"Reduced to {ResultCuboidCount:N0} cuboids ({optResult.ReductionPercentage:F1}% reduction).", NotificationSeverity.Success, "Voxelization");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing cuboids.");
            _notificationService.Publish("Greedy Optimization", ex.Message, NotificationSeverity.Error, "Voxelization");
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        SelectedMode = VoxelizationMode.SurfaceOnly;
        SelectedResolutionMode = VoxelResolutionMode.ByMaxGridDimension;
        VoxelSize = 1.0;
        MaxGridDimension = 64;
        SelectedColorSampling = ColorSamplingMode.MaterialDominance;
        SelectedAlignment = VoxelGridAlignment.CenterOnOrigin;
        RemoveIsolatedVoxels = false;
        FillInternalGaps = true;
        MergeAdjacentVoxels = true;
        PreserveMaterials = true;
        DefaultBlockNamespace = "minecraft:stone";
        _notificationService.Publish("Voxelization Settings", "Reset to default settings.", NotificationSeverity.Info, "Voxelization");
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Domain.Rules;
using NEXUS3D.Engine.Contracts;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Helpers;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel for the Converter workspace in NEXUS 3D.
/// Controls model inspection, profile selection, viewport state, data loss preview,
/// and single or batch format conversions through the Universal Scene Model (USM).
/// </summary>
public sealed partial class ConverterViewModel : BaseViewModel
{
    private readonly IConverterPreparationService _converterService;
    private readonly IFormatRegistry _formatRegistry;
    private readonly IConversionPipeline _conversionPipeline;
    private readonly IUniversalConversionPipeline _universalPipeline;
    private readonly IBatchConversionService _batchService;
    private readonly IBatchProcessingService _batchProcessingService;
    private readonly IConversionHistoryRepository _historyRepository;
    private readonly IDataLossAnalyzer _lossAnalyzer;
    private readonly IViewportController _viewportController;
    private readonly INotificationService _notificationService;
    private readonly IApplicationPathsService _pathsService;
    private readonly IRenderSceneAdapter _renderSceneAdapter;
    private readonly IEnumerable<IFormatParser> _parsers;
    private readonly IModelLoadingService _modelLoadingService;
    private readonly IWorkspaceSceneSession _workspaceSession;

    [ObservableProperty]
    private RenderScene? _previewScene;

    [ObservableProperty]
    private bool _isPreviewStale = true;

    [ObservableProperty]
    private bool _hasPreview = false;

    [ObservableProperty]
    private string _previewStatusText = "No 3D Preview Generated";

    [ObservableProperty]
    private string _sourceFilePath = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private ModelFormatType _selectedSourceFormat = ModelFormatType.Schematic;

    [ObservableProperty]
    private ModelFormatType _selectedTargetFormat = ModelFormatType.Obj;

    [ObservableProperty]
    private ConversionProfile? _selectedProfile;

    [ObservableProperty]
    private double _scaleFactor = 1.0;

    [ObservableProperty]
    private bool _optimizeMesh = true;

    [ObservableProperty]
    private bool _greedyMeshing = true;

    [ObservableProperty]
    private bool _preserveMaterials = true;

    [ObservableProperty]
    private MeshingAlgorithm _selectedMeshingMode = MeshingAlgorithm.GreedyMeshing;

    [ObservableProperty]
    private VoxelizationMode _selectedVoxelizationMode = VoxelizationMode.SurfaceOnly;

    [ObservableProperty]
    private double _targetVoxelSize = 1.0;

    [ObservableProperty]
    private bool _triangulatePolygons = false;

    [ObservableProperty]
    private bool _autoCorrectWindingOrder = true;

    [ObservableProperty]
    private bool _bakeBedrockBones = true;

    [ObservableProperty]
    private string _inspectedModelName = "No Model Selected";

    [ObservableProperty]
    private string _inspectedFileSize = "0 B";

    [ObservableProperty]
    private string _inspectedGeometryKind = "Unknown";

    [ObservableProperty]
    private string _engineStatusSummary = "Engine Ready";

    [ObservableProperty]
    private double _conversionProgress = 0.0;

    [ObservableProperty]
    private bool _isConverting = false;

    [ObservableProperty]
    private string _conversionProgressMessage = string.Empty;

    [ObservableProperty]
    private string _lossSummaryText = "No loss predicted";

    [ObservableProperty]
    private ViewportShadingMode _currentShadingMode = ViewportShadingMode.Textured;

    [ObservableProperty]
    private bool _showViewportGrid = true;

    [ObservableProperty]
    private int _selectedWorkspaceTab = 0;

    [ObservableProperty]
    private string _batchOutputDirectory = string.Empty;

    [ObservableProperty]
    private ModelFormatType _batchSelectedTargetFormat = ModelFormatType.Schematic;

    [ObservableProperty]
    private int _batchParallelism = 4;

    [ObservableProperty]
    private BatchJobPriority _batchPriority = BatchJobPriority.Normal;

    [ObservableProperty]
    private bool _batchOverwriteExisting = false;

    [ObservableProperty]
    private bool _batchContinueOnError = true;

    [ObservableProperty]
    private bool _batchEnableVoxelization = false;

    [ObservableProperty]
    private int _batchRetryLimit = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartBatch))]
    private bool _isBatchRunning = false;

    public bool CanStartBatch => !IsBatchRunning;

    [ObservableProperty]
    private bool _isBatchPaused = false;

    [ObservableProperty]
    private double _batchProgressPercentage = 0.0;

    [ObservableProperty]
    private string _batchProgressStatusText = "Batch queue idle";

    [ObservableProperty]
    private string _batchSummaryText = "No batch jobs executed yet";

    public ObservableCollection<BatchFileItemViewModel> BatchInputFiles { get; } = [];
    public ObservableCollection<BatchJobItemViewModel> BatchJobItems { get; } = [];

    public ObservableCollection<BatchJobPriority> AvailablePriorities { get; } =
    [
        BatchJobPriority.Critical,
        BatchJobPriority.High,
        BatchJobPriority.Normal,
        BatchJobPriority.Low
    ];

    private CancellationTokenSource? _batchCts;
    private BatchJobResult? _lastBatchResult;

    public ObservableCollection<ConversionProfile> Profiles { get; } = [];
    public ObservableCollection<ConversionLossItem> PredictedLossItems { get; } = [];
    public ObservableCollection<string> ExecutionLogs { get; } = [];

    public ObservableCollection<ModelFormatType> AvailableFormats { get; } =
    [
        ModelFormatType.Schematic,
        ModelFormatType.Obj,
        ModelFormatType.VoxelJson,
        ModelFormatType.BedrockGeometry
    ];

    public ObservableCollection<ViewportShadingMode> AvailableShadingModes { get; } =
    [
        ViewportShadingMode.Textured,
        ViewportShadingMode.Solid,
        ViewportShadingMode.Wireframe,
        ViewportShadingMode.VoxelGrid
    ];

    public ObservableCollection<MeshingAlgorithm> AvailableMeshingModes { get; } =
    [
        MeshingAlgorithm.GreedyMeshing,
        MeshingAlgorithm.ExposedFaceCulling,
        MeshingAlgorithm.SimpleBlockQuads
    ];

    public ConverterViewModel(
        IConverterPreparationService converterService,
        IFormatRegistry formatRegistry,
        IConversionPipeline conversionPipeline,
        IUniversalConversionPipeline universalPipeline,
        IBatchConversionService batchService,
        IBatchProcessingService batchProcessingService,
        IConversionHistoryRepository historyRepository,
        IDataLossAnalyzer lossAnalyzer,
        IViewportController viewportController,
        INotificationService notificationService,
        IApplicationPathsService pathsService,
        IRenderSceneAdapter renderSceneAdapter,
        IEnumerable<IFormatParser> parsers,
        IModelLoadingService modelLoadingService,
        IWorkspaceSceneSession workspaceSession)
    {
        _converterService = Guard.NotNull(converterService);
        _formatRegistry = Guard.NotNull(formatRegistry);
        _conversionPipeline = Guard.NotNull(conversionPipeline);
        _universalPipeline = Guard.NotNull(universalPipeline);
        _batchService = Guard.NotNull(batchService);
        _batchProcessingService = Guard.NotNull(batchProcessingService);
        _historyRepository = Guard.NotNull(historyRepository);
        _lossAnalyzer = Guard.NotNull(lossAnalyzer);
        _viewportController = Guard.NotNull(viewportController);
        _notificationService = Guard.NotNull(notificationService);
        _pathsService = Guard.NotNull(pathsService);
        _renderSceneAdapter = Guard.NotNull(renderSceneAdapter);
        _parsers = Guard.NotNull(parsers);
        _modelLoadingService = Guard.NotNull(modelLoadingService);
        _workspaceSession = Guard.NotNull(workspaceSession);

        Title = "3D Model Converter Workspace";
        Subtitle = "Universal multi-format conversion via Universal Scene Model (USM).";
        OutputDirectory = Path.Combine(_pathsService.AppDataRootDirectory, "Exports");
        BatchOutputDirectory = Path.Combine(_pathsService.AppDataRootDirectory, "BatchExports");

        var snapshot = _conversionPipeline.GetStatusSnapshot();
        EngineStatusSummary = $"{snapshot.EngineName} v{snapshot.EngineVersion} ({snapshot.MaxConcurrentJobs} Threads)";
    }

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken = default)
    {
        await LoadProfilesAsync(cancellationToken);

        // If no file is set in converter, but a model is active in workspace session, sync it!
        if (string.IsNullOrWhiteSpace(SourceFilePath) && !string.IsNullOrWhiteSpace(_workspaceSession.ActiveFilePath))
        {
            SourceFilePath = _workspaceSession.ActiveFilePath;
            await InspectSelectedFileAsync(cancellationToken);
        }
    }

    partial void OnSelectedProfileChanged(ConversionProfile? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedSourceFormat = value.SourceFormat;
        SelectedTargetFormat = value.TargetFormat;
        ScaleFactor = value.ScaleFactor;
        OptimizeMesh = value.OptimizeMesh;
        GreedyMeshing = value.GreedyMeshing;
        PreserveMaterials = value.PreserveMaterials;
        StatusMessage = $"Applied preset profile: {value.Name}";
    }

    partial void OnCurrentShadingModeChanged(ViewportShadingMode value)
    {
        _viewportController.SetShadingMode(value);
        StatusMessage = $"Viewport shading mode set to {value}";
    }

    partial void OnShowViewportGridChanged(bool value)
    {
        _viewportController.ToggleGrid(value);
    }

    [RelayCommand]
    private async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _converterService.GetProfilesAsync(cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            Profiles.Clear();
            foreach (var profile in result.Value)
            {
                Profiles.Add(profile);
            }

            if (SelectedProfile is null && Profiles.Count > 0)
            {
                SelectedProfile = Profiles[0];
            }
        }
    }

    [RelayCommand]
    private async Task BrowseSourceFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select 3D Model File",
            Filter = _formatRegistry.BuildFileDialogFilter(includeAllSupported: true),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            SourceFilePath = dialog.FileName;
            await InspectSelectedFileAsync();
        }
    }

    [RelayCommand]
    private async Task InspectSelectedFileAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SourceFilePath))
        {
            ErrorMessage = "Please enter or browse to a 3D model file path.";
            return;
        }

        ClearError();
        var detected = ConversionDomainRules.DetectFormatFromPath(SourceFilePath);
        if (detected != ModelFormatType.Unknown)
        {
            SelectedSourceFormat = detected;
        }

        var result = await _converterService.InspectModelFileAsync(SourceFilePath, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        InspectedModelName = result.Value.Name;
        InspectedFileSize = FileSizeFormatter.FormatBytes(result.Value.FileSizeBytes);
        InspectedGeometryKind = result.Value.GeometryKind.ToString();
        StatusMessage = $"Inspected '{result.Value.Name}' ({result.Value.Format})";
    }

    [RelayCommand]
    private async Task PrepareConversionAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            var request = new PrepareConversionRequest(
                SourceFilePath: SourceFilePath,
                OutputDirectory: OutputDirectory,
                SourceFormat: SelectedSourceFormat,
                TargetFormat: SelectedTargetFormat,
                ProfileId: SelectedProfile?.Id,
                ScaleFactor: ScaleFactor,
                OptimizeMesh: OptimizeMesh,
                GreedyMeshing: GreedyMeshing,
                PreserveMaterials: PreserveMaterials);

            var result = await _converterService.PrepareConversionJobAsync(request, cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                ErrorMessage = result.ErrorMessage;
                _notificationService.Publish(
                    "Conversion Validation Failed",
                    result.ErrorMessage,
                    NotificationSeverity.Warning,
                    "Converter");
                return;
            }

            StatusMessage = $"Prepared job: {result.Value.HistoryRecord.JobName}";
            _notificationService.Publish(
                "Conversion Job Prepared",
                $"Queued '{result.Value.HistoryRecord.JobName}' -> {result.Value.TargetFilePath}",
                NotificationSeverity.Success,
                "Converter");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExecuteConversionAsync(CancellationToken cancellationToken = default)
    {
        if (IsConverting || IsBusy) return;

        if (string.IsNullOrWhiteSpace(SourceFilePath) || !File.Exists(SourceFilePath))
        {
            ErrorMessage = "Please specify a valid source 3D model file.";
            return;
        }

        try
        {
            IsConverting = true;
            ClearError();
            ExecutionLogs.Clear();
            PredictedLossItems.Clear();
            ConversionProgress = 0.0;
            ConversionProgressMessage = "Initializing Universal Scene Model pipeline...";

            string baseFileName = Path.GetFileNameWithoutExtension(SourceFilePath);
            if (baseFileName.EndsWith(".geo", StringComparison.OrdinalIgnoreCase) ||
                baseFileName.EndsWith(".voxel", StringComparison.OrdinalIgnoreCase))
            {
                baseFileName = Path.GetFileNameWithoutExtension(baseFileName);
            }

            string targetExt = SelectedTargetFormat switch
            {
                ModelFormatType.Obj => ".obj",
                ModelFormatType.Schematic => ".schem",
                ModelFormatType.VoxelJson => ".voxel.json",
                ModelFormatType.BedrockGeometry => ".geo.json",
                _ => ".obj"
            };

            string outDir = string.IsNullOrWhiteSpace(OutputDirectory)
                ? Path.Combine(_pathsService.AppDataRootDirectory, "Exports")
                : OutputDirectory;

            Directory.CreateDirectory(outDir);
            string targetFilePath = Path.Combine(outDir, $"{baseFileName}{targetExt}");
            string jobName = $"{baseFileName} ({SelectedSourceFormat} -> {SelectedTargetFormat})";

            long sourceSize = new FileInfo(SourceFilePath).Length;
            var historyRecord = new ConversionHistoryRecord(
                jobName: jobName,
                sourceFilePath: SourceFilePath,
                targetFilePath: targetFilePath,
                sourceFormat: SelectedSourceFormat,
                targetFormat: SelectedTargetFormat,
                sourceSizeBytes: sourceSize,
                profileId: SelectedProfile?.Id);

            var options = new ConversionOptions
            {
                ScaleFactor = ScaleFactor,
                MeshingMode = GreedyMeshing ? MeshingAlgorithm.GreedyMeshing : SelectedMeshingMode,
                PreserveMaterials = PreserveMaterials,
                AutoCorrectWindingOrder = AutoCorrectWindingOrder,
                AdaptCoordinates = true,
                TriangulatePolygons = TriangulatePolygons,
                BakeBedrockBonesToMesh = BakeBedrockBones,
                TargetVoxelSize = TargetVoxelSize,
                VoxelizationMode = SelectedVoxelizationMode
            };

            var request = new ConversionRequest(
                SourceFilePath: SourceFilePath,
                TargetFilePath: targetFilePath,
                SourceFormat: SelectedSourceFormat,
                TargetFormat: SelectedTargetFormat,
                Options: options,
                ProfileId: SelectedProfile?.Id);

            var progressReporter = new Progress<double>(p =>
            {
                ConversionProgress = p * 100.0;
                ConversionProgressMessage = $"Converting ({ConversionProgress:F0}%)...";
            });

            ConversionResult result = await _universalPipeline.ExecuteAsync(request, progressReporter, cancellationToken);

            foreach (var log in result.Logs)
            {
                ExecutionLogs.Add(log);
            }

            foreach (var item in result.LossReport.Items)
            {
                PredictedLossItems.Add(item);
            }

            LossSummaryText = $"Loss Severity: {result.LossReport.OverallSeverity} ({result.LossReport.LostCount} lost, {result.LossReport.ApproximatedCount} approx, {result.LossReport.PreservedCount} preserved)";

            if (result.IsSuccess)
            {
                historyRecord.MarkCompleted(result.TargetSizeBytes, result.DurationMs);
                await _historyRepository.AddAsync(historyRecord, cancellationToken);

                StatusMessage = $"Conversion Succeeded in {result.DurationMs}ms -> {Path.GetFileName(result.TargetFilePath)}";
                _notificationService.Publish(
                    "Conversion Succeeded",
                    $"Generated {Path.GetFileName(result.TargetFilePath)} ({FileSizeFormatter.FormatBytes(result.TargetSizeBytes)}) in {result.DurationMs}ms.",
                    NotificationSeverity.Success,
                    "Converter");
            }
            else
            {
                historyRecord.MarkFailed(result.ErrorMessage ?? "Conversion failed", result.DurationMs);
                await _historyRepository.AddAsync(historyRecord, cancellationToken);

                ErrorMessage = result.ErrorMessage;
                StatusMessage = $"Conversion Failed: {result.ErrorMessage}";
                _notificationService.Publish(
                    "Conversion Failed",
                    result.ErrorMessage ?? "Unknown conversion failure.",
                    NotificationSeverity.Error,
                    "Converter");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = $"Conversion Error: {ex.Message}";
        }
        finally
        {
            IsConverting = false;
            ConversionProgress = 100.0;
        }
    }

    [RelayCommand]
    private void ResetViewportCamera()
    {
        _viewportController.ResetCamera();
        StatusMessage = "Viewport camera reset to default isometric orbit.";
    }

    partial void OnSourceFilePathChanged(string value)
    {
        IsPreviewStale = true;
        PreviewStatusText = "Source model changed • Preview is stale";
    }

    partial void OnSelectedTargetFormatChanged(ModelFormatType value)
    {
        IsPreviewStale = true;
        PreviewStatusText = "Target format changed • Preview is stale";
    }

    partial void OnSelectedMeshingModeChanged(MeshingAlgorithm value)
    {
        IsPreviewStale = true;
        PreviewStatusText = "Meshing algorithm changed • Preview is stale";
    }

    partial void OnTargetVoxelSizeChanged(double value)
    {
        IsPreviewStale = true;
        PreviewStatusText = "Target voxel size changed • Preview is stale";
    }

    [RelayCommand]
    public async Task GeneratePreviewAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SourceFilePath) || !File.Exists(SourceFilePath))
        {
            ErrorMessage = "Select a valid source model before generating 3D preview.";
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            StatusMessage = "Generating 3D conversion preview...";

            var loadResult = await _modelLoadingService.LoadModelAsync(SourceFilePath, cancellationToken);
            if (loadResult.IsFailure || loadResult.Value is null)
            {
                ErrorMessage = $"Failed to parse model for preview: {loadResult.ErrorMessage}";
                return;
            }

            var rScene = _renderSceneAdapter.Adapt(loadResult.Value, RenderSettings.Default);
            PreviewScene = rScene;
            IsPreviewStale = false;
            HasPreview = true;
            PreviewStatusText = $"Preview ready: {rScene.TotalTriangles:N0} tris, {rScene.TotalVoxels:N0} voxels";
            StatusMessage = "3D Preview updated successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Preview generation error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddBatchFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select 3D Models for Batch Conversion",
            Filter = "All 3D Models|*.schem;*.schematic;*.obj;*.json;*.geo.json;*.voxel.json|OBJ Models (*.obj)|*.obj|Minecraft Schematic (*.schem;*.schematic)|*.schem;*.schematic|Voxel JSON (*.voxel.json;*.json)|*.voxel.json;*.json|Bedrock Geometry (*.geo.json)|*.geo.json|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddBatchFilesFromPaths(dialog.FileNames);
        }
    }

    [RelayCommand]
    private void AddBatchFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder Containing 3D Models"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName) && Directory.Exists(dialog.FolderName))
        {
            var files = Directory.EnumerateFiles(dialog.FolderName, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".schem", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".schematic", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            AddBatchFilesFromPaths(files);
        }
    }

    public void AddBatchFilesFromPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path) && !BatchInputFiles.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                BatchInputFiles.Add(new BatchFileItemViewModel(path));
            }
        }
        StatusMessage = $"Batch input files: {BatchInputFiles.Count} models staged.";
    }

    [RelayCommand]
    private void RemoveBatchFile(BatchFileItemViewModel? item)
    {
        if (item is not null)
        {
            BatchInputFiles.Remove(item);
            StatusMessage = $"Removed '{item.FileName}'. Remaining: {BatchInputFiles.Count} files.";
        }
    }

    [RelayCommand]
    private void ClearBatchFiles()
    {
        BatchInputFiles.Clear();
        BatchJobItems.Clear();
        BatchProgressPercentage = 0.0;
        BatchProgressStatusText = "Batch queue cleared";
        StatusMessage = "Batch queue cleared.";
    }

    [RelayCommand]
    private void BrowseBatchOutputDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Batch Export Directory"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            BatchOutputDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void OpenBatchOutputFolder()
    {
        try
        {
            if (!Directory.Exists(BatchOutputDirectory))
            {
                Directory.CreateDirectory(BatchOutputDirectory);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = BatchOutputDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to open output directory: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartBatchAsync()
    {
        if (BatchInputFiles.Count == 0)
        {
            ErrorMessage = "Add at least one 3D model file to the batch queue.";
            return;
        }

        if (string.IsNullOrWhiteSpace(BatchOutputDirectory))
        {
            ErrorMessage = "Select a valid output destination directory.";
            return;
        }

        try
        {
            IsBatchRunning = true;
            IsBatchPaused = false;
            ClearError();
            BatchJobItems.Clear();
            BatchProgressPercentage = 0.0;
            BatchProgressStatusText = "Initializing batch queue...";

            Directory.CreateDirectory(BatchOutputDirectory);

            var fileList = BatchInputFiles.Select(f => f.FilePath).ToList();
            var options = new ConversionOptions
            {
                ScaleFactor = ScaleFactor,
                MeshingMode = GreedyMeshing ? MeshingAlgorithm.GreedyMeshing : SelectedMeshingMode,
                PreserveMaterials = PreserveMaterials,
                VoxelizationMode = BatchEnableVoxelization ? SelectedVoxelizationMode : VoxelizationMode.SurfaceOnly
            };

            var request = new BatchJobRequest(
                JobName: $"WPF-Batch-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                InputFiles: fileList,
                OutputDirectory: BatchOutputDirectory,
                TargetFormat: BatchSelectedTargetFormat,
                Priority: BatchPriority,
                Options: options,
                OverwriteExisting: BatchOverwriteExisting,
                ContinueOnError: BatchContinueOnError,
                RetryLimit: BatchRetryLimit,
                MaxConcurrency: BatchParallelism);

            var job = await _batchProcessingService.CreateJobAsync(request);

            foreach (var item in job.Items)
            {
                BatchJobItems.Add(new BatchJobItemViewModel(
                    item.ItemId,
                    item.SourceFilePath,
                    item.TargetFilePath,
                    item.SourceFormat,
                    item.TargetFormat));
            }

            _batchCts = new CancellationTokenSource();

            var progress = new Progress<BatchProgress>(report =>
            {
                BatchProgressPercentage = report.PercentComplete;
                BatchProgressStatusText = $"{report.PercentComplete:F0}% ({report.CompletedFiles}/{report.TotalFiles}) - {report.CurrentFileName}";

                foreach (var item in job.Items)
                {
                    var vm = BatchJobItems.FirstOrDefault(v => v.ItemId == item.ItemId);
                    if (vm is not null)
                    {
                        vm.UpdateStatus(item.Status, item.DurationMs, item.ErrorMessage);
                    }
                }
            });

            _lastBatchResult = await _batchProcessingService.StartJobAsync(job.JobId, progress, _batchCts.Token);

            BatchSummaryText = $"Completed: {_lastBatchResult.SucceededFiles}/{_lastBatchResult.TotalFiles} Succeeded, {_lastBatchResult.FailedFiles} Failed, {_lastBatchResult.SkippedFiles} Skipped in {_lastBatchResult.DurationMs:N0}ms";
            BatchProgressStatusText = $"Batch finished: {_lastBatchResult.Status}";

            _notificationService.Publish(
                "Batch Conversion Complete",
                $"{_lastBatchResult.SucceededFiles}/{_lastBatchResult.TotalFiles} files converted successfully in {_lastBatchResult.DurationMs}ms.",
                _lastBatchResult.FailedFiles == 0 ? NotificationSeverity.Success : NotificationSeverity.Warning,
                "Batch Converter");
        }
        catch (OperationCanceledException)
        {
            BatchProgressStatusText = "Batch conversion cancelled by user.";
            BatchSummaryText = "Batch cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Batch conversion error: {ex.Message}";
            BatchProgressStatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBatchRunning = false;
            _batchCts?.Dispose();
            _batchCts = null;
        }
    }

    [RelayCommand]
    private async Task CancelBatchAsync()
    {
        if (_batchCts is not null && !_batchCts.IsCancellationRequested)
        {
            _batchCts.Cancel();
            BatchProgressStatusText = "Cancelling batch operation...";
            StatusMessage = "Batch cancellation requested.";
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void PauseBatch()
    {
        IsBatchPaused = true;
        BatchProgressStatusText = "Batch execution paused.";
        StatusMessage = "Batch paused.";
    }

    [RelayCommand]
    private void ResumeBatch()
    {
        IsBatchPaused = false;
        BatchProgressStatusText = "Batch execution resumed.";
        StatusMessage = "Batch resumed.";
    }

    [RelayCommand]
    private void TogglePauseBatch()
    {
        IsBatchPaused = !IsBatchPaused;
        BatchProgressStatusText = IsBatchPaused ? "Batch execution paused." : "Batch execution resumed.";
        StatusMessage = IsBatchPaused ? "Batch paused." : "Batch resumed.";
    }

    [RelayCommand]
    private async Task RetryFailedBatchAsync()
    {
        var failedVms = BatchJobItems.Where(i => i.Status == BatchItemStatus.Failed).ToList();
        if (failedVms.Count == 0)
        {
            StatusMessage = "No failed items to retry.";
            return;
        }

        var failedPaths = failedVms.Select(v => v.SourceFilePath).ToList();
        BatchInputFiles.Clear();
        AddBatchFilesFromPaths(failedPaths);
        await StartBatchAsync();
    }

    [RelayCommand]
    private void ClearCompletedBatch()
    {
        var toRemove = BatchJobItems.Where(i => i.Status is BatchItemStatus.Succeeded or BatchItemStatus.Skipped).ToList();
        foreach (var item in toRemove)
        {
            BatchJobItems.Remove(item);
        }
        StatusMessage = $"Cleared {toRemove.Count} completed items.";
    }

    [RelayCommand]
    private async Task ExportBatchJsonReportAsync()
    {
        if (_lastBatchResult is null)
        {
            ErrorMessage = "Run a batch job first before exporting a report.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save Batch Execution JSON Report",
            Filter = "JSON Files (*.json)|*.json",
            FileName = $"batch-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_lastBatchResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(dialog.FileName, json);
                StatusMessage = $"JSON report saved to '{Path.GetFileName(dialog.FileName)}'.";
                _notificationService.Publish("Report Saved", $"Batch report saved to {dialog.FileName}", NotificationSeverity.Success, "Batch Converter");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save report: {ex.Message}";
            }
        }
    }
}

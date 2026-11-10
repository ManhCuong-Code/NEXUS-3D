using System.Collections.ObjectModel;
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
    private readonly IConversionHistoryRepository _historyRepository;
    private readonly IDataLossAnalyzer _lossAnalyzer;
    private readonly IViewportController _viewportController;
    private readonly INotificationService _notificationService;
    private readonly IApplicationPathsService _pathsService;
    private readonly IRenderSceneAdapter _renderSceneAdapter;
    private readonly IEnumerable<IFormatParser> _parsers;

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
        IConversionHistoryRepository historyRepository,
        IDataLossAnalyzer lossAnalyzer,
        IViewportController viewportController,
        INotificationService notificationService,
        IApplicationPathsService pathsService,
        IRenderSceneAdapter renderSceneAdapter,
        IEnumerable<IFormatParser> parsers)
    {
        _converterService = Guard.NotNull(converterService);
        _formatRegistry = Guard.NotNull(formatRegistry);
        _conversionPipeline = Guard.NotNull(conversionPipeline);
        _universalPipeline = Guard.NotNull(universalPipeline);
        _batchService = Guard.NotNull(batchService);
        _historyRepository = Guard.NotNull(historyRepository);
        _lossAnalyzer = Guard.NotNull(lossAnalyzer);
        _viewportController = Guard.NotNull(viewportController);
        _notificationService = Guard.NotNull(notificationService);
        _pathsService = Guard.NotNull(pathsService);
        _renderSceneAdapter = Guard.NotNull(renderSceneAdapter);
        _parsers = Guard.NotNull(parsers);

        Title = "3D Model Converter Workspace";
        Subtitle = "Universal multi-format conversion via Universal Scene Model (USM).";
        OutputDirectory = Path.Combine(_pathsService.AppDataRootDirectory, "Exports");

        var snapshot = _conversionPipeline.GetStatusSnapshot();
        EngineStatusSummary = $"{snapshot.EngineName} v{snapshot.EngineVersion} ({snapshot.MaxConcurrentJobs} Threads)";
    }

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken = default)
    {
        await LoadProfilesAsync(cancellationToken);
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

            string ext = Path.GetExtension(SourceFilePath).ToLowerInvariant();
            var parser = _parsers.FirstOrDefault(p =>
                (ext is ".schem" or ".schematic" && p.FormatIdentifier.Contains("schematic", StringComparison.OrdinalIgnoreCase)) ||
                (ext is ".obj" && p.FormatIdentifier.Contains("obj", StringComparison.OrdinalIgnoreCase)) ||
                (ext is ".json" or ".voxel.json" && p.FormatIdentifier.Contains("voxel", StringComparison.OrdinalIgnoreCase)) ||
                (ext is ".geo.json" && p.FormatIdentifier.Contains("bedrock", StringComparison.OrdinalIgnoreCase)) ||
                p.FormatIdentifier.Contains(ext.TrimStart('.'), StringComparison.OrdinalIgnoreCase));

            if (parser is null)
            {
                ErrorMessage = $"No parser available for previewing format '{ext}'.";
                return;
            }

            using var stream = File.OpenRead(SourceFilePath);
            var parseResult = await parser.ParseToSceneAsync(
                stream,
                new ParseOptions { StrictValidation = false, PreserveUnknownData = true },
                cancellationToken);

            if (parseResult.IsFailure || parseResult.Value is null)
            {
                ErrorMessage = $"Failed to parse model for preview: {parseResult.Error.Message}";
                return;
            }

            var rScene = _renderSceneAdapter.Adapt(parseResult.Value, RenderSettings.Default);
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
}

using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Rules;
using NEXUS3D.Engine.Contracts;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Helpers;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel for the Converter preparation workspace.
/// Allows selecting source 3D models, inspecting metadata, configuring conversion profiles,
/// controlling foundational viewport state, and queuing conversion tasks.
/// </summary>
public sealed partial class ConverterViewModel : BaseViewModel
{
    private readonly IConverterPreparationService _converterService;
    private readonly IFormatRegistry _formatRegistry;
    private readonly IConversionPipeline _conversionPipeline;
    private readonly IViewportController _viewportController;
    private readonly INotificationService _notificationService;
    private readonly IApplicationPathsService _pathsService;

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
    private string _inspectedModelName = "No Model Selected";

    [ObservableProperty]
    private string _inspectedFileSize = "0 B";

    [ObservableProperty]
    private string _inspectedGeometryKind = "Unknown";

    [ObservableProperty]
    private string _engineStatusSummary = "Engine Ready";

    [ObservableProperty]
    private ViewportShadingMode _currentShadingMode = ViewportShadingMode.Textured;

    [ObservableProperty]
    private bool _showViewportGrid = true;

    public ObservableCollection<ConversionProfile> Profiles { get; } = [];
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

    public ConverterViewModel(
        IConverterPreparationService converterService,
        IFormatRegistry formatRegistry,
        IConversionPipeline conversionPipeline,
        IViewportController viewportController,
        INotificationService notificationService,
        IApplicationPathsService pathsService)
    {
        _converterService = Guard.NotNull(converterService);
        _formatRegistry = Guard.NotNull(formatRegistry);
        _conversionPipeline = Guard.NotNull(conversionPipeline);
        _viewportController = Guard.NotNull(viewportController);
        _notificationService = Guard.NotNull(notificationService);
        _pathsService = Guard.NotNull(pathsService);

        Title = "3D Model Converter Workspace";
        Subtitle = "Configure format pipelines, inspect model headers, and prepare conversion jobs.";
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
        if (IsBusy)
        {
            return;
        }

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
    private void ResetViewportCamera()
    {
        _viewportController.ResetCamera();
        StatusMessage = "Viewport camera reset to default isometric orbit.";
    }
}

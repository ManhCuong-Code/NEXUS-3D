using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.App.ViewModels;

public sealed partial class ConvertModelDialogViewModel : ObservableObject
{
    private readonly Scene3D _scene;
    private readonly IConversionCompatibilityService _compatibilityService;
    private readonly IConversionExecutor _conversionExecutor;

    [ObservableProperty]
    private string _sourceModelName;

    [ObservableProperty]
    private string _sourceFilePath;

    [ObservableProperty]
    private ModelFormatType _sourceFormat;

    [ObservableProperty]
    private string _sourceFormatName;

    [ObservableProperty]
    private string _geometrySummary = string.Empty;

    [ObservableProperty]
    private TargetFormatCompatibility? _selectedTarget;

    [ObservableProperty]
    private string _destinationFilePath = string.Empty;

    [ObservableProperty]
    private bool _isConverting;

    public bool CanConvert => !IsConverting;

    partial void OnIsConvertingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanConvert));
    }

    [ObservableProperty]
    private double _conversionProgress;

    [ObservableProperty]
    private string _conversionStatus = "Ready to convert";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _resultDetails = string.Empty;

    public ObservableCollection<TargetFormatCompatibility> TargetOptions { get; } = [];

    public event EventHandler<string>? OpenConvertedFileRequested;
    public event EventHandler? RequestClose;

    public ConvertModelDialogViewModel(
        Scene3D scene,
        string sourceFilePath,
        ModelFormatType sourceFormat,
        IConversionCompatibilityService compatibilityService,
        IConversionExecutor conversionExecutor)
    {
        _scene = scene;
        _sourceFilePath = sourceFilePath;
        _sourceFormat = sourceFormat;
        _compatibilityService = compatibilityService;
        _conversionExecutor = conversionExecutor;

        _sourceModelName = scene.Name;
        _sourceFormatName = sourceFormat.ToString();

        // Calculate geometry summary
        int meshCount = 0;
        int triCount = 0;
        int voxelCount = 0;
        foreach (var node in scene.Graph.Nodes)
        {
            if (node.Object is not { } obj) continue;
            meshCount += obj.Meshes.Count;
            triCount += obj.Meshes.Sum(m => m.Faces.Count);
            voxelCount += obj.VoxelGrids.Sum(g => g.GetVoxelCount());
        }

        if (triCount > 0)
        {
            GeometrySummary = $"{triCount:N0} Triangles ({meshCount} Meshes)";
        }
        else if (voxelCount > 0)
        {
            GeometrySummary = $"{voxelCount:N0} Voxels / Blocks";
        }
        else
        {
            GeometrySummary = "Universal Scene Model";
        }

        LoadTargets();
    }

    private void LoadTargets()
    {
        TargetOptions.Clear();
        var targets = _compatibilityService.GetCompatibleTargets(_scene, SourceFormat);
        foreach (var t in targets)
        {
            TargetOptions.Add(t);
        }

        if (TargetOptions.Count > 0)
        {
            SelectedTarget = TargetOptions[0];
            UpdateDefaultDestinationPath();
        }
    }

    partial void OnSelectedTargetChanged(TargetFormatCompatibility? value)
    {
        UpdateDefaultDestinationPath();
    }

    private void UpdateDefaultDestinationPath()
    {
        if (SelectedTarget is null) return;
        string dir = !string.IsNullOrWhiteSpace(SourceFilePath) && File.Exists(SourceFilePath)
            ? Path.GetDirectoryName(SourceFilePath)!
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        string baseName = Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(SourceFilePath) ? SourceModelName : SourceFilePath);
        string ext = SelectedTarget.PrimaryExtension;
        DestinationFilePath = Path.Combine(dir, $"{baseName}_converted{ext}");
    }

    [RelayCommand]
    public void BrowseDestination()
    {
        if (SelectedTarget is null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Select Destination Path",
            FileName = Path.GetFileName(DestinationFilePath),
            Filter = $"{SelectedTarget.DisplayName} (*{SelectedTarget.PrimaryExtension})|*{SelectedTarget.PrimaryExtension}|All Files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(DestinationFilePath)
        };

        if (sfd.ShowDialog() == true)
        {
            DestinationFilePath = sfd.FileName;
        }
    }

    [RelayCommand]
    public async Task ConvertNowAsync()
    {
        if (SelectedTarget is null)
        {
            ErrorMessage = "Please select a target format.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DestinationFilePath))
        {
            ErrorMessage = "Please specify a valid destination file path.";
            return;
        }

        try
        {
            IsConverting = true;
            ErrorMessage = null;
            ConversionStatus = $"Converting '{SourceModelName}' to {SelectedTarget.DisplayName}...";
            ConversionProgress = 10;

            var request = new ConversionRequest(
                SourceFilePath: SourceFilePath,
                TargetFilePath: DestinationFilePath,
                SourceFormat: SourceFormat,
                TargetFormat: SelectedTarget.FormatType,
                Options: ConversionOptions.Default);

            var progress = new Progress<double>(p =>
            {
                ConversionProgress = Math.Clamp(p * 100.0, 10.0, 95.0);
            });

            var result = await _conversionExecutor.ExecuteAsync(request, progress);

            ConversionProgress = 100;
            if (result.IsSuccess)
            {
                IsCompleted = true;
                ConversionStatus = "Conversion completed successfully!";
                ResultDetails = $"File written to: {DestinationFilePath}\n" +
                                $"Time elapsed: {result.DurationMs:N0} ms\n" +
                                $"Target Format: {SelectedTarget.DisplayName}\n" +
                                $"Loss Level: {SelectedTarget.RatingBadge}";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Conversion failed.";
                ConversionStatus = "Failed to convert.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ConversionStatus = "Conversion error.";
        }
        finally
        {
            IsConverting = false;
        }
    }

    [RelayCommand]
    public void OpenInNewWorkspace()
    {
        if (File.Exists(DestinationFilePath))
        {
            OpenConvertedFileRequested?.Invoke(this, DestinationFilePath);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}

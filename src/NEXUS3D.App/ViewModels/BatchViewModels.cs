using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Helpers;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// UI view model for an input file staged for batch conversion.
/// </summary>
public sealed partial class BatchFileItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _extension = string.Empty;

    [ObservableProperty]
    private ModelFormatType _detectedFormat = ModelFormatType.Unknown;

    [ObservableProperty]
    private long _fileSizeBytes = 0;

    [ObservableProperty]
    private string _formattedSize = "0 B";

    public BatchFileItemViewModel(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (File.Exists(filePath))
        {
            FileSizeBytes = new FileInfo(filePath).Length;
            FormattedSize = FileSizeFormatter.FormatBytes(FileSizeBytes);
        }

        DetectedFormat = DetectFormat(filePath);
    }

    private static ModelFormatType DetectFormat(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        if (name.EndsWith(".geo.json", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.BedrockGeometry;
        if (name.EndsWith(".voxel.json", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.VoxelJson;

        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".obj" => ModelFormatType.Obj,
            ".schem" or ".schematic" => ModelFormatType.Schematic,
            ".json" => ModelFormatType.VoxelJson,
            _ => ModelFormatType.Unknown
        };
    }
}

/// <summary>
/// UI view model representing an individual task inside an active batch conversion job.
/// </summary>
public sealed partial class BatchJobItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _itemId;

    [ObservableProperty]
    private string _sourceFilePath = string.Empty;

    [ObservableProperty]
    private string _sourceFileName = string.Empty;

    [ObservableProperty]
    private string _targetFilePath = string.Empty;

    [ObservableProperty]
    private ModelFormatType _sourceFormat;

    [ObservableProperty]
    private ModelFormatType _targetFormat;

    [ObservableProperty]
    private BatchItemStatus _status = BatchItemStatus.Pending;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private long _durationMs;

    [ObservableProperty]
    private string _durationDisplay = "0 ms";

    public BatchJobItemViewModel(
        Guid itemId,
        string sourceFilePath,
        string targetFilePath,
        ModelFormatType sourceFormat,
        ModelFormatType targetFormat)
    {
        ItemId = itemId;
        SourceFilePath = sourceFilePath;
        SourceFileName = Path.GetFileName(sourceFilePath);
        TargetFilePath = targetFilePath;
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
    }

    public void UpdateStatus(BatchItemStatus status, long durationMs, string? error = null)
    {
        Status = status;
        DurationMs = durationMs;
        DurationDisplay = $"{durationMs} ms";
        ErrorMessage = error;
    }
}

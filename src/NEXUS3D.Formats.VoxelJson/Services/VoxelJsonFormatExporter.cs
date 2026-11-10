using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Conversion;
using NEXUS3D.Formats.VoxelJson.Exporting;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Formats.VoxelJson.Services;

public interface IVoxelJsonWriter : IFormatExporter, IVoxelJsonExporter
{
    Task<FormatExportReport> ExportSceneToFileAsync(
        Scene3D scene,
        string outputFilePath,
        VoxelJsonWriteOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// High-level Voxel JSON exporter service implementing <see cref="IVoxelJsonWriter"/>, <see cref="IFormatExporter"/>, and <see cref="IVoxelJsonExporter"/>.
/// </summary>
public sealed class VoxelJsonFormatExporter : IVoxelJsonWriter
{
    private readonly ISceneToVoxelJsonConverter _sceneToVoxelConverter;
    private readonly VoxelJsonExporter _exporter;
    private readonly VoxelJsonParser _parserForVerification;

    public string FormatIdentifier => "voxel:json";
    public ModelFormatType TargetFormat => ModelFormatType.VoxelJson;
    public bool PrettyPrintOutput => true;

    public VoxelJsonFormatExporter(
        ISceneToVoxelJsonConverter? sceneToVoxelConverter = null,
        VoxelJsonExporter? exporter = null,
        VoxelJsonParser? parserForVerification = null)
    {
        _sceneToVoxelConverter = sceneToVoxelConverter ?? new SceneToVoxelJsonConverter();
        _exporter = exporter ?? new VoxelJsonExporter();
        _parserForVerification = parserForVerification ?? new VoxelJsonParser();
    }

    public Task<FormatExportReport> ExportSceneToFileAsync(
        Scene3D scene,
        string outputFilePath,
        VoxelJsonWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        cancellationToken.ThrowIfCancellationRequested();

        options ??= VoxelJsonWriteOptions.Default;

        try
        {
            var mappingOptions = new VoxelMappingOptions
            {
                PreserveRawData = options.WriteExtensionData,
                TargetDialect = options.TargetDialect,
                ExportOptions = options
            };

            var (voxelDoc, convReport) = _sceneToVoxelConverter.Convert(scene, mappingOptions);
            _exporter.ExportToFile(voxelDoc, outputFilePath, options);

            long bytesWritten = new FileInfo(outputFilePath).Length;
            return Task.FromResult(new FormatExportReport(
                IsSuccess: true,
                BytesWritten: bytesWritten,
                LossReport: convReport.LossReport,
                Warnings: convReport.Warnings));
        }
        catch (JsonFormatException ex)
        {
            return Task.FromResult(new FormatExportReport(
                IsSuccess: false,
                BytesWritten: 0,
                LossReport: new LossReport(),
                Warnings: [],
                Error: new Error(ex.ErrorCode, ex.Message)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(new FormatExportReport(
                IsSuccess: false,
                BytesWritten: 0,
                LossReport: new LossReport(),
                Warnings: [],
                Error: new Error(JsonFormatErrorCodes.ExportFailed, ex.Message)));
        }
    }

    public async Task<FormatExportReport> ExportSceneAsync(
        Scene3D scene,
        Stream destination,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            var writeOptions = new VoxelJsonWriteOptions
            {
                PrettyPrint = PrettyPrintOutput,
                WriteExtensionData = options?.PreserveUnknownData ?? true,
                VerifyAfterWrite = options?.VerifyAfterWrite ?? true
            };

            var mappingOptions = new VoxelMappingOptions
            {
                StrictMode = options?.StrictMapping ?? false,
                PreserveRawData = options?.PreserveUnknownData ?? true,
                ExportOptions = writeOptions
            };

            var (voxelDoc, convReport) = _sceneToVoxelConverter.Convert(scene, mappingOptions);

            using var ms = new MemoryStream();
            _exporter.ExportToStream(voxelDoc, ms, writeOptions);

            if (writeOptions.VerifyAfterWrite)
            {
                ms.Position = 0;
                VoxelJsonParseResult verified = _parserForVerification.Parse(ms, new VoxelJsonReadOptions
                {
                    StrictMode = false,
                    DuplicatePolicy = DuplicateVoxelPolicy.KeepAll
                });

                if (verified.HasErrors || verified.Document.Voxels.Count != voxelDoc.Voxels.Count)
                {
                    return new FormatExportReport(
                        IsSuccess: false,
                        BytesWritten: 0,
                        LossReport: convReport.LossReport,
                        Warnings: convReport.Warnings,
                        Error: new Error(JsonFormatErrorCodes.ExportFailed, "Stream read-back verification failed after Voxel JSON export."));
                }
            }

            ms.Position = 0;
            await ms.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

            return new FormatExportReport(
                IsSuccess: true,
                BytesWritten: ms.Length,
                LossReport: convReport.LossReport,
                Warnings: convReport.Warnings);
        }
        catch (JsonFormatException ex)
        {
            return new FormatExportReport(
                IsSuccess: false,
                BytesWritten: 0,
                LossReport: new LossReport(),
                Warnings: [],
                Error: new Error(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new FormatExportReport(
                IsSuccess: false,
                BytesWritten: 0,
                LossReport: new LossReport(),
                Warnings: [],
                Error: new Error(JsonFormatErrorCodes.ExportFailed, ex.Message));
        }
    }

    public Task<OperationResult> ValidateTargetAsync(
        string outputFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return Task.FromResult(OperationResult.Failure(JsonFormatErrorCodes.ExportFailed, "Output file path cannot be empty."));
        }

        var descriptor = new VoxelJsonFormatDescriptor();
        if (!descriptor.CanHandleExtension(outputFilePath))
        {
            return Task.FromResult(OperationResult.Failure(
                JsonFormatErrorCodes.ExportFailed,
                $"File '{outputFilePath}' does not have a supported Voxel JSON extension (.json, .voxel.json)."));
        }

        return Task.FromResult(OperationResult.Success());
    }
}

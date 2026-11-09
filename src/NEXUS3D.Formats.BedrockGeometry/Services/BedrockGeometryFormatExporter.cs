using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Exporting;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Formats.BedrockGeometry.Services;

public interface IBedrockGeometryWriter : IFormatExporter, IBedrockGeometryExporter
{
    Task<FormatExportReport> ExportSceneToFileAsync(
        Scene3D scene,
        string outputFilePath,
        BedrockGeometryWriteOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// High-level Minecraft Bedrock Geometry exporter service implementing <see cref="IBedrockGeometryWriter"/>,
/// <see cref="IFormatExporter"/>, and <see cref="IBedrockGeometryExporter"/>.
/// </summary>
public sealed class BedrockGeometryFormatExporter : IBedrockGeometryWriter
{
    private readonly ISceneToBedrockGeometryConverter _sceneToBedrockConverter;
    private readonly BedrockGeometryExporter _exporter;
    private readonly BedrockGeometryParser _parserForVerification;

    public string FormatIdentifier => "minecraft:bedrock_geometry";
    public ModelFormatType TargetFormat => ModelFormatType.BedrockGeometry;
    public string TargetFormatVersion => "1.12.0";

    public BedrockGeometryFormatExporter(
        ISceneToBedrockGeometryConverter? sceneToBedrockConverter = null,
        BedrockGeometryExporter? exporter = null,
        BedrockGeometryParser? parserForVerification = null)
    {
        _sceneToBedrockConverter = sceneToBedrockConverter ?? new SceneToBedrockGeometryConverter();
        _exporter = exporter ?? new BedrockGeometryExporter();
        _parserForVerification = parserForVerification ?? new BedrockGeometryParser();
    }

    public Task<FormatExportReport> ExportSceneToFileAsync(
        Scene3D scene,
        string outputFilePath,
        BedrockGeometryWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        cancellationToken.ThrowIfCancellationRequested();

        options ??= BedrockGeometryWriteOptions.Default;

        try
        {
            var mappingOptions = new BedrockMappingOptions
            {
                PreserveRawData = options.WriteExtensionData,
                TargetFormatVersion = options.TargetFormatVersion,
                ExportOptions = options
            };

            var (bedrockDoc, convReport) = _sceneToBedrockConverter.Convert(scene, mappingOptions);
            _exporter.ExportToFile(bedrockDoc, outputFilePath, options);

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
            var writeOptions = new BedrockGeometryWriteOptions
            {
                PrettyPrint = true,
                WriteExtensionData = options?.PreserveUnknownData ?? true,
                VerifyAfterWrite = options?.VerifyAfterWrite ?? true
            };

            var mappingOptions = new BedrockMappingOptions
            {
                StrictMode = options?.StrictMapping ?? false,
                PreserveRawData = options?.PreserveUnknownData ?? true,
                ExportOptions = writeOptions
            };

            var (bedrockDoc, convReport) = _sceneToBedrockConverter.Convert(scene, mappingOptions);

            using var ms = new MemoryStream();
            _exporter.ExportToStream(bedrockDoc, ms, writeOptions);

            if (writeOptions.VerifyAfterWrite)
            {
                ms.Position = 0;
                BedrockGeometryParseResult verified = _parserForVerification.Parse(ms, new BedrockGeometryReadOptions
                {
                    StrictMode = false
                });

                if (verified.HasErrors || verified.Document.Geometries.Count != bedrockDoc.Geometries.Count)
                {
                    return new FormatExportReport(
                        IsSuccess: false,
                        BytesWritten: 0,
                        LossReport: convReport.LossReport,
                        Warnings: convReport.Warnings,
                        Error: new Error(JsonFormatErrorCodes.ExportFailed, "Stream read-back verification failed after Bedrock Geometry export."));
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

        string normalized = outputFilePath.Trim().ToLowerInvariant();
        if (!normalized.EndsWith(".geo.json", StringComparison.Ordinal) &&
            !normalized.EndsWith(".json", StringComparison.Ordinal))
        {
            return Task.FromResult(OperationResult.Failure(
                JsonFormatErrorCodes.ExportFailed,
                $"File '{outputFilePath}' does not have a supported Bedrock Geometry extension (.geo.json, .json)."));
        }

        return Task.FromResult(OperationResult.Success());
    }
}

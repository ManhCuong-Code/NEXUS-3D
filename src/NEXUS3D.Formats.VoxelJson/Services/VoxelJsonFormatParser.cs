using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Conversion;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Formats.VoxelJson.Services;

public sealed class VoxelJsonReadResult
{
    public bool IsSuccess { get; init; }
    public VoxelDocument? Document { get; init; }
    public Scene3D? Scene { get; init; }
    public VoxelConversionReport? ConversionReport { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    public Error? Error { get; init; }

    public static VoxelJsonReadResult Failure(Error error, IReadOnlyList<ValidationIssue>? issues = null) =>
        new()
        {
            IsSuccess = false,
            Error = error,
            Issues = issues ?? []
        };
}

public interface IVoxelJsonReader : IFormatParser, IVoxelJsonParser
{
    Task<VoxelJsonReadResult> ReadFileAsync(
        string filePath,
        VoxelMappingOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<VoxelJsonReadResult> ReadAsync(
        Stream source,
        VoxelMappingOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// High-level Voxel JSON parser service implementing <see cref="IVoxelJsonReader"/>, <see cref="IFormatParser"/>, and <see cref="IVoxelJsonParser"/>.
/// </summary>
public sealed class VoxelJsonFormatParser : IVoxelJsonReader
{
    private readonly VoxelJsonParser _parser;
    private readonly IVoxelJsonToSceneConverter _converter;

    public string FormatIdentifier => "voxel:json";
    public ModelFormatType SupportedFormat => ModelFormatType.VoxelJson;
    public int MaxAllowedElements => 2_000_000;

    public VoxelJsonFormatParser(
        VoxelJsonParser? parser = null,
        IVoxelJsonToSceneConverter? converter = null)
    {
        _parser = parser ?? new VoxelJsonParser();
        _converter = converter ?? new VoxelJsonToSceneConverter();
    }

    public async Task<VoxelJsonReadResult> ReadFileAsync(
        string filePath,
        VoxelMappingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return VoxelJsonReadResult.Failure(
                new Error(JsonFormatErrorCodes.EmptyOrInvalidRoot, $"Voxel JSON file not found: '{filePath}'."));
        }

        string fullPath = Path.GetFullPath(filePath);
        long fileSize = new FileInfo(fullPath).Length;
        var effectiveOptions = (options ?? VoxelMappingOptions.Default) with
        {
            SourceFileName = Path.GetFileName(fullPath),
            SourceFileSizeBytes = fileSize
        };

        await using var fs = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        return await ReadAsync(fs, effectiveOptions, cancellationToken).ConfigureAwait(false);
    }

    public Task<VoxelJsonReadResult> ReadAsync(
        Stream source,
        VoxelMappingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? VoxelMappingOptions.Default;

        try
        {
            var readOptions = new VoxelJsonReadOptions
            {
                StrictMode = effectiveOptions.StrictMode,
                PreserveExtensionData = effectiveOptions.PreserveRawData,
                DuplicatePolicy = effectiveOptions.DuplicatePolicy,
                SourceFileName = effectiveOptions.SourceFileName
            };

            VoxelJsonParseResult parseResult = _parser.Parse(source, readOptions, effectiveOptions.SourceFileName);
            var (scene, report) = _converter.Convert(parseResult.Document, effectiveOptions, parseResult.Diagnostics);

            if (effectiveOptions.StrictMode)
            {
                ValidationResult sceneValidation = scene.Validate();
                if (!sceneValidation.IsValid)
                {
                    ValidationIssue firstErr = sceneValidation.Issues.First(i => i.Severity >= ValidationSeverity.Error);
                    return Task.FromResult(VoxelJsonReadResult.Failure(
                        new Error(firstErr.Code, firstErr.Message),
                        report.Issues));
                }
            }

            return Task.FromResult(new VoxelJsonReadResult
            {
                IsSuccess = true,
                Document = parseResult.Document,
                Scene = scene,
                ConversionReport = report,
                Issues = report.Issues
            });
        }
        catch (JsonFormatException ex)
        {
            return Task.FromResult(VoxelJsonReadResult.Failure(new Error(ex.ErrorCode, ex.Message)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(VoxelJsonReadResult.Failure(new Error(JsonFormatErrorCodes.SyntaxError, ex.Message)));
        }
    }

    public async Task<Result<Scene3D>> ParseToSceneAsync(
        Stream source,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var mappingOptions = new VoxelMappingOptions
        {
            StrictMode = options?.StrictValidation ?? false,
            PreserveRawData = options?.PreserveUnknownData ?? true,
            SourceFileName = options?.SourceFileName
        };

        VoxelJsonReadResult readResult = await ReadAsync(source, mappingOptions, cancellationToken).ConfigureAwait(false);
        return readResult.IsSuccess && readResult.Scene is not null
            ? Result<Scene3D>.Success(readResult.Scene)
            : Result<Scene3D>.Failure(readResult.Error ?? new Error(JsonFormatErrorCodes.SyntaxError, "Failed to parse Voxel JSON stream."));
    }

    public async Task<OperationResult<ModelDocumentSummary>> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        VoxelJsonReadResult readResult = await ReadFileAsync(
            filePath,
            VoxelMappingOptions.Default with { StrictMode = false },
            cancellationToken).ConfigureAwait(false);

        if (!readResult.IsSuccess || readResult.Scene is null)
        {
            return OperationResult<ModelDocumentSummary>.Failure(
                readResult.Error?.Code ?? JsonFormatErrorCodes.SyntaxError,
                readResult.Error?.Message ?? "Failed to inspect Voxel JSON file.");
        }

        var stats = readResult.Scene.CalculateStatistics();
        var fileInfo = new FileInfo(filePath);
        var summary = new ModelDocumentSummary(
            name: fileInfo.Name,
            filePath: fileInfo.FullName,
            format: ModelFormatType.VoxelJson,
            geometryKind: GeometryKind.VoxelGrid,
            fileSizeBytes: fileInfo.Exists ? fileInfo.Length : 0,
            elementCount: stats.TotalVoxelCount);

        return OperationResult<ModelDocumentSummary>.Success(summary);
    }
}

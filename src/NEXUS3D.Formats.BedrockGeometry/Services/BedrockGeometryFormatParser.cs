using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Formats.BedrockGeometry.Services;

public sealed class BedrockGeometryReadResult
{
    public bool IsSuccess { get; init; }
    public BedrockGeometryDocument? Document { get; init; }
    public Scene3D? Scene { get; init; }
    public BedrockConversionReport? ConversionReport { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    public Error? Error { get; init; }

    public static BedrockGeometryReadResult Failure(Error error, IReadOnlyList<ValidationIssue>? issues = null) =>
        new()
        {
            IsSuccess = false,
            Error = error,
            Issues = issues ?? []
        };
}

public interface IBedrockGeometryReader : IFormatParser, IBedrockGeometryParser
{
    Task<BedrockGeometryReadResult> ReadFileAsync(
        string filePath,
        BedrockMappingOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<BedrockGeometryReadResult> ReadAsync(
        Stream source,
        BedrockMappingOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// High-level Minecraft Bedrock Geometry parser service implementing <see cref="IBedrockGeometryReader"/>,
/// <see cref="IFormatParser"/>, and <see cref="IBedrockGeometryParser"/>.
/// </summary>
public sealed class BedrockGeometryFormatParser : IBedrockGeometryReader
{
    private readonly BedrockGeometryParser _parser;
    private readonly IBedrockGeometryToSceneConverter _converter;

    public string FormatIdentifier => "minecraft:bedrock_geometry";
    public ModelFormatType SupportedFormat => ModelFormatType.BedrockGeometry;
    public string DefaultFormatVersion => "1.12.0";

    public BedrockGeometryFormatParser(
        BedrockGeometryParser? parser = null,
        IBedrockGeometryToSceneConverter? converter = null)
    {
        _parser = parser ?? new BedrockGeometryParser();
        _converter = converter ?? new BedrockGeometryToSceneConverter();
    }

    public async Task<BedrockGeometryReadResult> ReadFileAsync(
        string filePath,
        BedrockMappingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return BedrockGeometryReadResult.Failure(
                new Error(JsonFormatErrorCodes.EmptyOrInvalidRoot, $"Bedrock Geometry file not found: '{filePath}'."));
        }

        string fullPath = Path.GetFullPath(filePath);
        long fileSize = new FileInfo(fullPath).Length;
        var effectiveOptions = (options ?? BedrockMappingOptions.Default) with
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

    public Task<BedrockGeometryReadResult> ReadAsync(
        Stream source,
        BedrockMappingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? BedrockMappingOptions.Default;

        try
        {
            var readOptions = new BedrockGeometryReadOptions
            {
                StrictMode = effectiveOptions.StrictMode,
                PreserveExtensionData = effectiveOptions.PreserveRawData,
                SourceFileName = effectiveOptions.SourceFileName
            };

            BedrockGeometryParseResult parseResult = _parser.Parse(source, readOptions, effectiveOptions.SourceFileName);
            var (scene, report) = _converter.Convert(parseResult.Document, effectiveOptions, parseResult.Diagnostics);

            if (effectiveOptions.StrictMode)
            {
                ValidationResult sceneValidation = scene.Validate();
                if (!sceneValidation.IsValid)
                {
                    ValidationIssue firstErr = sceneValidation.Issues.First(i => i.Severity >= ValidationSeverity.Error);
                    return Task.FromResult(BedrockGeometryReadResult.Failure(
                        new Error(firstErr.Code, firstErr.Message),
                        report.Issues));
                }
            }

            return Task.FromResult(new BedrockGeometryReadResult
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
            return Task.FromResult(BedrockGeometryReadResult.Failure(new Error(ex.ErrorCode, ex.Message)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(BedrockGeometryReadResult.Failure(new Error(JsonFormatErrorCodes.SyntaxError, ex.Message)));
        }
    }

    public async Task<Result<Scene3D>> ParseToSceneAsync(
        Stream source,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var mappingOptions = new BedrockMappingOptions
        {
            StrictMode = options?.StrictValidation ?? false,
            PreserveRawData = options?.PreserveUnknownData ?? true,
            SourceFileName = options?.SourceFileName
        };

        BedrockGeometryReadResult readResult = await ReadAsync(source, mappingOptions, cancellationToken).ConfigureAwait(false);
        return readResult.IsSuccess && readResult.Scene is not null
            ? Result<Scene3D>.Success(readResult.Scene)
            : Result<Scene3D>.Failure(readResult.Error ?? new Error(JsonFormatErrorCodes.SyntaxError, "Failed to parse Bedrock Geometry stream."));
    }

    public async Task<OperationResult<ModelDocumentSummary>> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        BedrockGeometryReadResult readResult = await ReadFileAsync(
            filePath,
            BedrockMappingOptions.Default with { StrictMode = false },
            cancellationToken).ConfigureAwait(false);

        if (!readResult.IsSuccess || readResult.Scene is null)
        {
            return OperationResult<ModelDocumentSummary>.Failure(
                readResult.Error?.Code ?? JsonFormatErrorCodes.SyntaxError,
                readResult.Error?.Message ?? "Failed to inspect Bedrock Geometry file.");
        }

        int totalCubes = readResult.ConversionReport?.CubeCount ?? 0;
        var fileInfo = new FileInfo(filePath);
        var summary = new ModelDocumentSummary(
            name: fileInfo.Name,
            filePath: fileInfo.FullName,
            format: ModelFormatType.BedrockGeometry,
            geometryKind: GeometryKind.CuboidHierarchy,
            fileSizeBytes: fileInfo.Exists ? fileInfo.Length : 0,
            elementCount: totalCubes);

        return OperationResult<ModelDocumentSummary>.Success(summary);
    }
}

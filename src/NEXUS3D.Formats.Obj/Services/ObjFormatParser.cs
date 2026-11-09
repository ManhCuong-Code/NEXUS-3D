using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Conversion;
using NEXUS3D.Formats.Obj.Materials;
using NEXUS3D.Formats.Obj.Parsing;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Formats.Obj.Services;

/// <summary>
/// Outcome of reading a Wavefront OBJ file (and optional companion MTL library) into USM <see cref="Scene3D"/>.
/// </summary>
public sealed class ObjReadResult
{
    public bool IsSuccess { get; init; }
    public ObjDocument? ObjDocument { get; init; }
    public MtlDocument? MtlDocument { get; init; }
    public Scene3D? Scene { get; init; }
    public ObjConversionReport? ConversionReport { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    public Error? Error { get; init; }

    public static ObjReadResult Failure(Error error, IReadOnlyList<ValidationIssue>? issues = null) =>
        new()
        {
            IsSuccess = false,
            Error = error,
            Issues = issues ?? []
        };
}

/// <summary>
/// Unified contract for reading Wavefront OBJ files/streams into <see cref="ObjDocument"/>, <see cref="MtlDocument"/>,
/// and Universal Scene Model (<see cref="Scene3D"/>).
/// </summary>
public interface IObjReader : IFormatParser, IObjParser
{
    Task<ObjReadResult> ReadFileAsync(
        string filePath,
        ObjMappingOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<ObjReadResult> ReadAsync(
        Stream source,
        ObjMappingOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// High-level Wavefront OBJ &amp; MTL parser service implementing <see cref="IObjReader"/>, <see cref="IFormatParser"/>, and <see cref="IObjParser"/>.
/// </summary>
public sealed class ObjFormatParser : IObjReader
{
    private readonly IObjDocumentParser _objParser;
    private readonly IMtlParser _mtlParser;
    private readonly IObjToSceneConverter _sceneConverter;

    public string FormatIdentifier => "wavefront:obj";
    public ModelFormatType SupportedFormat => ModelFormatType.Obj;
    public bool ResolveCompanionMtl => true;

    public ObjFormatParser(
        IObjDocumentParser? objParser = null,
        IMtlParser? mtlParser = null,
        IObjToSceneConverter? sceneConverter = null)
    {
        _objParser = objParser ?? new ObjParser();
        _mtlParser = mtlParser ?? new MtlParser();
        _sceneConverter = sceneConverter ?? new ObjToSceneConverter();
    }

    public async Task<ObjReadResult> ReadFileAsync(
        string filePath,
        ObjMappingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return ObjReadResult.Failure(
                new Error(ObjErrorCodes.EmptyOrUnrecognizedFile, $"OBJ file not found: '{filePath}'."));
        }

        string fullPath = Path.GetFullPath(filePath);
        string? baseDir = Path.GetDirectoryName(fullPath);
        long fileSize = new FileInfo(fullPath).Length;

        var effectiveOptions = (options ?? ObjMappingOptions.Default) with
        {
            SourceFileName = Path.GetFileName(fullPath),
            SourceFileSizeBytes = fileSize,
            BaseDirectory = options?.BaseDirectory ?? baseDir
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

    public async Task<ObjReadResult> ReadAsync(
        Stream source,
        ObjMappingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var effectiveOptions = options ?? ObjMappingOptions.Default;
        var allIssues = new List<ValidationIssue>();

        try
        {
            var parseOptions = new ObjParseOptions
            {
                StrictValidation = effectiveOptions.StrictMode,
                TriangulatePolygons = false, // ObjToSceneConverter handles triangulation if requested
                PreserveUnrecognizedStatements = effectiveOptions.PreserveRawData,
                SourceFilePath = effectiveOptions.SourceFileName,
                BaseDirectory = effectiveOptions.BaseDirectory
            };

            ObjDocument objDoc = await _objParser.ParseDocumentAsync(source, parseOptions, cancellationToken).ConfigureAwait(false);
            MtlDocument? combinedMtl = null;

            if (effectiveOptions.LoadCompanionMtl &&
                objDoc.MaterialLibraries.Count > 0 &&
                !string.IsNullOrWhiteSpace(effectiveOptions.BaseDirectory))
            {
                combinedMtl = new MtlDocument();
                var mtlOptions = new MtlParseOptions
                {
                    StrictMode = effectiveOptions.StrictMode,
                    PreserveComments = effectiveOptions.PreserveRawData,
                    PreserveCustomProperties = effectiveOptions.PreserveRawData
                };

                foreach (string mtlRelPath in objDoc.MaterialLibraries)
                {
                    if (string.IsNullOrWhiteSpace(mtlRelPath))
                    {
                        continue;
                    }

                    string normalizedMtlPath = mtlRelPath
                        .Replace('/', Path.DirectorySeparatorChar)
                        .Replace('\\', Path.DirectorySeparatorChar);

                    string fullMtlPath = Path.GetFullPath(Path.Combine(effectiveOptions.BaseDirectory!, normalizedMtlPath));
                    combinedMtl.SourcePath ??= fullMtlPath;

                    MtlParseResult mtlResult = await _mtlParser.ParseFileAsync(fullMtlPath, mtlOptions, cancellationToken).ConfigureAwait(false);
                    allIssues.AddRange(mtlResult.Issues);
                    combinedMtl.HeaderComments.AddRange(mtlResult.Document.HeaderComments);
                    combinedMtl.Materials.AddRange(mtlResult.Document.Materials);
                }
            }

            var (scene, report) = _sceneConverter.Convert(objDoc, combinedMtl, effectiveOptions);
            allIssues.AddRange(report.Issues);

            if (effectiveOptions.StrictMode)
            {
                ValidationResult sceneValidation = scene.Validate();
                if (!sceneValidation.IsValid)
                {
                    ValidationIssue firstErr = sceneValidation.Issues.First(i => i.Severity >= ValidationSeverity.Error);
                    return ObjReadResult.Failure(
                        new Error(firstErr.Code, firstErr.Message),
                        allIssues);
                }
            }

            return new ObjReadResult
            {
                IsSuccess = true,
                ObjDocument = objDoc,
                MtlDocument = combinedMtl,
                Scene = scene,
                ConversionReport = report,
                Issues = allIssues
            };
        }
        catch (ObjException ex)
        {
            return ObjReadResult.Failure(new Error(ex.ErrorCode, ex.Message), allIssues);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ObjReadResult.Failure(new Error(ObjErrorCodes.SyntaxError, ex.Message), allIssues);
        }
    }

    public async Task<Result<Scene3D>> ParseToSceneAsync(
        Stream source,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string? baseDir = null;
        if (!string.IsNullOrWhiteSpace(options?.SourceFileName) && Path.IsPathRooted(options.SourceFileName))
        {
            baseDir = Path.GetDirectoryName(options.SourceFileName);
        }

        var mappingOptions = new ObjMappingOptions
        {
            StrictMode = options?.StrictValidation ?? false,
            PreserveRawData = options?.PreserveUnknownData ?? true,
            SourceFileName = options?.SourceFileName,
            BaseDirectory = baseDir
        };

        ObjReadResult readResult = await ReadAsync(source, mappingOptions, cancellationToken).ConfigureAwait(false);
        return readResult.IsSuccess && readResult.Scene is not null
            ? Result<Scene3D>.Success(readResult.Scene)
            : Result<Scene3D>.Failure(readResult.Error ?? new Error(ObjErrorCodes.SyntaxError, "Failed to parse OBJ stream."));
    }

    public async Task<OperationResult<ModelDocumentSummary>> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ObjReadResult readResult = await ReadFileAsync(
            filePath,
            ObjMappingOptions.Default with { StrictMode = false },
            cancellationToken).ConfigureAwait(false);

        if (!readResult.IsSuccess || readResult.Scene is null)
        {
            return OperationResult<ModelDocumentSummary>.Failure(
                readResult.Error?.Code ?? ObjErrorCodes.SyntaxError,
                readResult.Error?.Message ?? "Failed to inspect OBJ file.");
        }

        var stats = readResult.Scene.CalculateStatistics();
        var fileInfo = new FileInfo(filePath);
        var summary = new ModelDocumentSummary(
            name: fileInfo.Name,
            filePath: fileInfo.FullName,
            format: ModelFormatType.Obj,
            geometryKind: GeometryKind.PolygonMesh,
            fileSizeBytes: fileInfo.Exists ? fileInfo.Length : 0,
            elementCount: stats.TotalFaceCount > 0 ? stats.TotalFaceCount : stats.TotalVertexCount);

        return OperationResult<ModelDocumentSummary>.Success(summary);
    }
}

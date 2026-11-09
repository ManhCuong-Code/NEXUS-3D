using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Conversion;
using NEXUS3D.Formats.Schematic.Legacy;
using NEXUS3D.Formats.Schematic.Nbt;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Sponge;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Formats.Schematic.Services;

/// <summary>
/// Unified contract for reading Minecraft Schematic files/streams into <see cref="SchematicDocument"/>
/// and Universal Scene Model <see cref="Scene3D"/>.
/// </summary>
public interface ISchematicReader : IFormatParser, ISchematicParser
{
    Task<SchematicReadResult> ReadAsync(
        Stream source,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<SchematicReadResult> ReadFileAsync(
        string filePath,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<SchematicDocument> ReadDocumentAsync(
        Stream source,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Unified Schematic parser supporting Sponge v2, Sponge v3, and Legacy MCEdit <c>.schematic</c> files,
/// automatically converting parsed documents into USM <see cref="Scene3D"/>.
/// </summary>
public sealed class SchematicFormatParser : ISchematicReader
{
    private readonly INbtReader _nbtReader;
    private readonly ISchematicFormatDetector _detector;
    private readonly ISpongeV2Parser _spongeV2Parser;
    private readonly ISpongeV3Parser _spongeV3Parser;
    private readonly ILegacySchematicParser _legacyParser;
    private readonly ISchematicToSceneConverter _sceneConverter;

    public string FormatIdentifier => "minecraft:schematic";
    public ModelFormatType SupportedFormat => ModelFormatType.Schematic;
    public IReadOnlyList<int> SupportedDataVersions { get; } = [1343, 1976, 2586, 2975, 3465, 3953];

    public SchematicFormatParser(
        INbtReader? nbtReader = null,
        ISchematicFormatDetector? detector = null,
        ISpongeV2Parser? spongeV2Parser = null,
        ISpongeV3Parser? spongeV3Parser = null,
        ILegacySchematicParser? legacyParser = null,
        ISchematicToSceneConverter? sceneConverter = null)
    {
        _nbtReader = nbtReader ?? new NbtReader();
        _detector = detector ?? new SchematicFormatDetector(_nbtReader);
        _spongeV2Parser = spongeV2Parser ?? new SpongeV2Parser();
        _spongeV3Parser = spongeV3Parser ?? new SpongeV3Parser();
        _legacyParser = legacyParser ?? new LegacySchematicParser();
        _sceneConverter = sceneConverter ?? new SchematicToSceneConverter();
    }

    public async Task<SchematicDocument> ReadDocumentAsync(
        Stream source,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var effectiveOptions = options ?? SchematicReadOptions.Default;

        NbtDocument nbtDoc = await _nbtReader.ReadAsync(source, effectiveOptions.NbtOptions, cancellationToken).ConfigureAwait(false);
        SchematicFormatId formatId = _detector.DetectFromDocument(nbtDoc, out int version);

        if (formatId == SchematicFormatId.Unknown)
        {
            if (version != 0)
            {
                throw new SchematicException(
                    SchematicErrorCodes.UnsupportedVersion,
                    $"Sponge Schematic Version {version} is not supported (only Version 2 and Version 3 are supported).");
            }

            throw new SchematicException(
                SchematicErrorCodes.UnrecognizedSchematic,
                "The NBT document does not match Sponge v2, Sponge v3, or Legacy Schematic structure.");
        }

        return formatId switch
        {
            SchematicFormatId.SpongeV2 => await _spongeV2Parser.ParseAsync(nbtDoc, effectiveOptions, cancellationToken).ConfigureAwait(false),
            SchematicFormatId.SpongeV3 => await _spongeV3Parser.ParseAsync(nbtDoc, effectiveOptions, cancellationToken).ConfigureAwait(false),
            SchematicFormatId.LegacyMcEdit => await _legacyParser.ParseAsync(nbtDoc, effectiveOptions, cancellationToken).ConfigureAwait(false),
            _ => throw new SchematicException(SchematicErrorCodes.UnrecognizedSchematic, $"Unsupported schematic format '{formatId}'.")
        };
    }

    public async Task<SchematicReadResult> ReadAsync(
        Stream source,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var effectiveOptions = options ?? SchematicReadOptions.Default;

        try
        {
            long sourceBytes = source.CanSeek ? source.Length - source.Position : 0;
            SchematicDocument doc = await ReadDocumentAsync(source, effectiveOptions, cancellationToken).ConfigureAwait(false);

            var mappingOptions = new SchematicMappingOptions
            {
                IncludeAirVoxels = effectiveOptions.IncludeAirVoxelsInUsm,
                PreserveRawNbtData = effectiveOptions.PreserveUnknownData,
                StrictLegacyMapping = effectiveOptions.StrictLegacyMapping,
                SourceFileName = effectiveOptions.SourceFileName,
                SourceFileSizeBytes = sourceBytes
            };

            var (scene, report) = _sceneConverter.Convert(doc, mappingOptions);

            if (effectiveOptions.StrictValidation)
            {
                var validation = scene.Validate();
                if (!validation.IsValid)
                {
                    var err = validation.Issues.First();
                    return SchematicReadResult.Failure(
                        new Error(err.Code, err.Message),
                        doc.Format,
                        report.Warnings);
                }
            }

            return SchematicReadResult.Success(
                doc.Format,
                doc,
                scene,
                report.Warnings,
                report.LossReport);
        }
        catch (SchematicException ex)
        {
            return SchematicReadResult.Failure(new Error(ex.ErrorCode, ex.Message));
        }
        catch (NbtException ex)
        {
            return SchematicReadResult.Failure(new Error(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SchematicReadResult.Failure(new Error(SchematicErrorCodes.UnrecognizedSchematic, ex.Message));
        }
    }

    public async Task<SchematicReadResult> ReadFileAsync(
        string filePath,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return SchematicReadResult.Failure(
                new Error(SchematicErrorCodes.MissingRequiredData, $"Schematic file not found: '{filePath}'."));
        }

        var effectiveOptions = (options ?? SchematicReadOptions.Default) with
        {
            SourceFileName = Path.GetFileName(filePath)
        };

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        return await ReadAsync(fs, effectiveOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<Scene3D>> ParseToSceneAsync(
        Stream source,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var schemOptions = new SchematicReadOptions
        {
            StrictValidation = options?.StrictValidation ?? true,
            PreserveUnknownData = options?.PreserveUnknownData ?? true,
            SourceFileName = options?.SourceFileName,
            NbtOptions = new NbtReadOptions
            {
                MaxDecompressedBytes = options?.MaxInputBytes ?? NbtReadOptions.Default.MaxDecompressedBytes,
                MaxNestingDepth = options?.MaxNestingDepth ?? NbtReadOptions.Default.MaxNestingDepth
            }
        };

        var readResult = await ReadAsync(source, schemOptions, cancellationToken).ConfigureAwait(false);
        return readResult.IsSuccess && readResult.Scene is not null
            ? Result<Scene3D>.Success(readResult.Scene)
            : Result<Scene3D>.Failure(readResult.Error ?? new Error(SchematicErrorCodes.UnrecognizedSchematic, "Failed to parse schematic."));
    }

    public async Task<OperationResult<ModelDocumentSummary>> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var result = await ReadFileAsync(filePath, SchematicReadOptions.Default, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Document is null || result.Scene is null)
        {
            return OperationResult<ModelDocumentSummary>.Failure(
                result.Error?.Code ?? SchematicErrorCodes.UnrecognizedSchematic,
                result.Error?.Message ?? "Failed to inspect schematic file.");
        }

        var stats = result.Scene.CalculateStatistics();
        var fileInfo = new FileInfo(filePath);
        var summary = new ModelDocumentSummary(
            name: fileInfo.Name,
            filePath: fileInfo.FullName,
            format: ModelFormatType.Schematic,
            geometryKind: GeometryKind.VoxelGrid,
            fileSizeBytes: fileInfo.Exists ? fileInfo.Length : 0,
            elementCount: stats.TotalVoxelCount);

        return OperationResult<ModelDocumentSummary>.Success(summary);
    }
}

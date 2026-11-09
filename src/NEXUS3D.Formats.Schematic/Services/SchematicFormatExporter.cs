using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Conversion;
using NEXUS3D.Formats.Schematic.Legacy;
using NEXUS3D.Formats.Schematic.Nbt;
using NEXUS3D.Formats.Schematic.Nbt.Compression;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Sponge;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Formats.Schematic.Services;

/// <summary>
/// Unified contract for exporting USM <see cref="Scene3D"/> or <see cref="SchematicDocument"/>
/// to Sponge v2, Sponge v3, or Legacy <c>.schematic</c> streams and files with safe atomic verification.
/// </summary>
public interface ISchematicWriter : IFormatExporter, ISchematicExporter
{
    Task<SchematicWriteResult> WriteSceneAsync(
        Scene3D scene,
        Stream destination,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<SchematicWriteResult> WriteDocumentAsync(
        SchematicDocument document,
        Stream destination,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<SchematicWriteResult> WriteSceneToFileAsync(
        Scene3D scene,
        string outputFilePath,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<SchematicWriteResult> WriteDocumentToFileAsync(
        SchematicDocument document,
        string outputFilePath,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Unified Schematic exporter supporting Sponge v2, Sponge v3, and Legacy MCEdit <c>.schematic</c>.
/// Implements safe temporary file writing and read-back verification before replacing any target file (Section 11.4).
/// </summary>
public sealed class SchematicFormatExporter : ISchematicWriter
{
    private readonly INbtWriter _nbtWriter;
    private readonly ISpongeV2Exporter _spongeV2Exporter;
    private readonly ISpongeV3Exporter _spongeV3Exporter;
    private readonly ILegacySchematicExporter _legacyExporter;
    private readonly ISceneToSchematicConverter _sceneToSchematicConverter;
    private readonly ISchematicReader _readerForVerification;

    public string FormatIdentifier => "minecraft:schematic";
    public ModelFormatType TargetFormat => ModelFormatType.Schematic;
    public bool CompressWithGZip => true;

    public SchematicFormatExporter(
        INbtWriter? nbtWriter = null,
        ISpongeV2Exporter? spongeV2Exporter = null,
        ISpongeV3Exporter? spongeV3Exporter = null,
        ILegacySchematicExporter? legacyExporter = null,
        ISceneToSchematicConverter? sceneToSchematicConverter = null,
        ISchematicReader? readerForVerification = null)
    {
        _nbtWriter = nbtWriter ?? new NbtWriter();
        _spongeV2Exporter = spongeV2Exporter ?? new SpongeV2Exporter();
        _spongeV3Exporter = spongeV3Exporter ?? new SpongeV3Exporter();
        _legacyExporter = legacyExporter ?? new LegacySchematicExporter();
        _sceneToSchematicConverter = sceneToSchematicConverter ?? new SceneToSchematicConverter();
        _readerForVerification = readerForVerification ?? new SchematicFormatParser();
    }

    public async Task<SchematicWriteResult> WriteSceneAsync(
        Scene3D scene,
        Stream destination,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(destination);

        var effectiveOptions = options ?? SchematicWriteOptions.Default;

        try
        {
            var mappingOptions = new SchematicMappingOptions
            {
                TargetFormat = effectiveOptions.TargetFormat,
                DefaultDataVersion = effectiveOptions.DataVersion,
                PreserveRawNbtData = effectiveOptions.PreserveUnknownData,
                StrictLegacyMapping = effectiveOptions.StrictMapping
            };

            var (doc, _) = _sceneToSchematicConverter.Convert(scene, mappingOptions);
            return await WriteDocumentAsync(doc, destination, effectiveOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (SchematicException ex)
        {
            return SchematicWriteResult.Failure(new Error(ex.ErrorCode, ex.Message), effectiveOptions.TargetFormat);
        }
        catch (NbtException ex)
        {
            return SchematicWriteResult.Failure(new Error(ex.ErrorCode, ex.Message), effectiveOptions.TargetFormat);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SchematicWriteResult.Failure(new Error(SchematicErrorCodes.ExportFailed, ex.Message), effectiveOptions.TargetFormat);
        }
    }

    public async Task<SchematicWriteResult> WriteDocumentAsync(
        SchematicDocument document,
        Stream destination,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);

        var effectiveOptions = options ?? SchematicWriteOptions.Default;

        try
        {
            NbtDocument nbtDoc = effectiveOptions.TargetFormat switch
            {
                SchematicFormatId.SpongeV2 => await _spongeV2Exporter.ExportAsync(document, effectiveOptions, cancellationToken).ConfigureAwait(false),
                SchematicFormatId.SpongeV3 => await _spongeV3Exporter.ExportAsync(document, effectiveOptions, cancellationToken).ConfigureAwait(false),
                SchematicFormatId.LegacyMcEdit => await _legacyExporter.ExportAsync(document, effectiveOptions, cancellationToken).ConfigureAwait(false),
                _ => throw new SchematicException(
                    SchematicErrorCodes.ExportFailed,
                    $"Unsupported schematic target format '{effectiveOptions.TargetFormat}'.")
            };

            var nbtWriteOptions = new NbtWriteOptions
            {
                Compression = effectiveOptions.Compression,
                Endianness = NbtEndianness.BigEndian
            };

            using var memoryBuffer = new MemoryStream();
            await _nbtWriter.WriteAsync(nbtDoc, memoryBuffer, nbtWriteOptions, cancellationToken).ConfigureAwait(false);

            bool verified = false;
            if (effectiveOptions.VerifyTemporaryOutputBeforeCommit)
            {
                memoryBuffer.Position = 0;
                var verifiedDoc = await _readerForVerification.ReadDocumentAsync(
                    memoryBuffer,
                    SchematicReadOptions.Default,
                    cancellationToken).ConfigureAwait(false);

                if (verifiedDoc.Dimensions != document.Dimensions)
                {
                    throw new SchematicException(
                        SchematicErrorCodes.ExportFailed,
                        "Post-write verification failed: exported schematic dimensions did not match source document.");
                }

                verified = true;
            }

            memoryBuffer.Position = 0;
            await memoryBuffer.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

            int unmappedBlocks = document.Warnings.Count(w => w.Code == SchematicErrorCodes.UnmappedLegacyId);

            return new SchematicWriteResult
            {
                IsSuccess = true,
                TargetFormat = effectiveOptions.TargetFormat,
                BytesWritten = memoryBuffer.Length,
                ExportedBlockCount = document.BlockIndices.Length,
                UnmappedBlockCount = unmappedBlocks,
                ExportedBlockEntityCount = document.BlockEntities.Count,
                UnsupportedBlockEntityCount = 0,
                VerifiedAfterWrite = verified,
                Warnings = document.Warnings.ToList()
            };
        }
        catch (SchematicException ex)
        {
            return SchematicWriteResult.Failure(
                new Error(ex.ErrorCode, ex.Message),
                effectiveOptions.TargetFormat,
                warnings: document.Warnings);
        }
        catch (NbtException ex)
        {
            return SchematicWriteResult.Failure(
                new Error(ex.ErrorCode, ex.Message),
                effectiveOptions.TargetFormat,
                warnings: document.Warnings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SchematicWriteResult.Failure(
                new Error(SchematicErrorCodes.ExportFailed, ex.Message),
                effectiveOptions.TargetFormat,
                warnings: document.Warnings);
        }
    }

    public async Task<SchematicWriteResult> WriteSceneToFileAsync(
        Scene3D scene,
        string outputFilePath,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var effectiveOptions = options ?? SchematicWriteOptions.Default;

        try
        {
            var mappingOptions = new SchematicMappingOptions
            {
                TargetFormat = effectiveOptions.TargetFormat,
                DefaultDataVersion = effectiveOptions.DataVersion,
                PreserveRawNbtData = effectiveOptions.PreserveUnknownData,
                StrictLegacyMapping = effectiveOptions.StrictMapping
            };

            var (doc, _) = _sceneToSchematicConverter.Convert(scene, mappingOptions);
            return await WriteDocumentToFileAsync(doc, outputFilePath, effectiveOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (SchematicException ex)
        {
            return SchematicWriteResult.Failure(new Error(ex.ErrorCode, ex.Message), effectiveOptions.TargetFormat);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SchematicWriteResult.Failure(new Error(SchematicErrorCodes.ExportFailed, ex.Message), effectiveOptions.TargetFormat);
        }
    }

    public async Task<SchematicWriteResult> WriteDocumentToFileAsync(
        SchematicDocument document,
        string outputFilePath,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return SchematicWriteResult.Failure(
                new Error(SchematicErrorCodes.ExportFailed, "Output file path cannot be empty."),
                (options ?? SchematicWriteOptions.Default).TargetFormat);
        }

        var effectiveOptions = options ?? SchematicWriteOptions.Default;
        string fullTargetPath = Path.GetFullPath(outputFilePath);

        if (File.Exists(fullTargetPath) && !effectiveOptions.AllowOverwrite)
        {
            return SchematicWriteResult.Failure(
                new Error(SchematicErrorCodes.ExportFailed, $"Target file '{fullTargetPath}' already exists and AllowOverwrite is false."),
                effectiveOptions.TargetFormat);
        }

        string? directory = Path.GetDirectoryName(fullTargetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Safe Atomic File Export (Section 11.4):
        // 1. Write to a temporary file in the same directory
        // 2. Verify temporary file by reading it back
        // 3. Atomically replace/move into the destination path only after verification succeeds
        string tempFilePath = $"{fullTargetPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            SchematicWriteResult writeResult;
            await using (var tempStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, useAsync: true))
            {
                writeResult = await WriteDocumentAsync(
                    document,
                    tempStream,
                    effectiveOptions with { VerifyTemporaryOutputBeforeCommit = false },
                    cancellationToken).ConfigureAwait(false);

                if (!writeResult.IsSuccess)
                {
                    return writeResult;
                }

                await tempStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (effectiveOptions.VerifyTemporaryOutputBeforeCommit)
            {
                var readBack = await _readerForVerification.ReadFileAsync(
                    tempFilePath,
                    SchematicReadOptions.Default,
                    cancellationToken).ConfigureAwait(false);

                if (!readBack.IsSuccess || readBack.Document is null || readBack.Document.Dimensions != document.Dimensions)
                {
                    throw new SchematicException(
                        SchematicErrorCodes.ExportFailed,
                        readBack.Error?.Message ?? "Temporary file verification failed after write.");
                }

                writeResult = writeResult with { VerifiedAfterWrite = true };
            }

            File.Move(tempFilePath, fullTargetPath, overwrite: effectiveOptions.AllowOverwrite);
            return writeResult;
        }
        catch (SchematicException ex)
        {
            return SchematicWriteResult.Failure(new Error(ex.ErrorCode, ex.Message), effectiveOptions.TargetFormat);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SchematicWriteResult.Failure(new Error(SchematicErrorCodes.ExportFailed, ex.Message), effectiveOptions.TargetFormat);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore best-effort temp cleanup failure
                }
            }
        }
    }

    public async Task<FormatExportReport> ExportSceneAsync(
        Scene3D scene,
        Stream destination,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var writeOptions = new SchematicWriteOptions
        {
            TargetFormat = SchematicFormatId.SpongeV2,
            StrictMapping = options?.StrictMapping ?? false,
            PreserveUnknownData = options?.PreserveUnknownData ?? true,
            VerifyTemporaryOutputBeforeCommit = options?.VerifyAfterWrite ?? true,
            AllowOverwrite = options?.AllowOverwrite ?? true,
            Compression = NbtCompressionType.GZip
        };

        var result = await WriteSceneAsync(scene, destination, writeOptions, cancellationToken).ConfigureAwait(false);
        return new FormatExportReport(
            result.IsSuccess,
            result.BytesWritten,
            result.LossReport,
            result.Warnings,
            result.Error);
    }

    public Task<OperationResult> ValidateTargetAsync(
        string outputFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return Task.FromResult(OperationResult.Failure(SchematicErrorCodes.ExportFailed, "Output file path cannot be empty."));
        }

        string ext = Path.GetExtension(outputFilePath);
        if (!ext.Equals(".schem", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".schematic", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(OperationResult.Failure(
                SchematicErrorCodes.ExportFailed,
                $"Extension '{ext}' is not a supported schematic extension (.schem or .schematic)."));
        }

        return Task.FromResult(OperationResult.Success());
    }
}

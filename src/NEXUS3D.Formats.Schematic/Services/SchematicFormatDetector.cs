using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Legacy;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Sponge;

namespace NEXUS3D.Formats.Schematic.Services;

/// <summary>
/// Contract for inspecting a stream, file, or parsed <see cref="NbtDocument"/> to determine which
/// Minecraft Schematic format variant it represents.
/// </summary>
public interface ISchematicFormatDetector : IFormatDetector
{
    Task<SchematicFormatId> DetectSchematicFormatAsync(
        Stream source,
        NbtReadOptions? nbtOptions = null,
        CancellationToken cancellationToken = default);

    SchematicFormatId DetectFromDocument(NbtDocument document, out int version);
}

/// <summary>
/// Content-based detector for Sponge Schematic v2, Sponge Schematic v3, and Legacy MCEdit <c>.schematic</c>.
/// Never relies solely on file extension (<c>.schem</c> vs <c>.schematic</c>).
/// </summary>
public sealed class SchematicFormatDetector : ISchematicFormatDetector
{
    private readonly INbtReader _nbtReader;

    public SchematicFormatDetector(INbtReader? nbtReader = null)
    {
        _nbtReader = nbtReader ?? new NbtReader();
    }

    public async Task<FormatDetectionResult> DetectAsync(
        Stream source,
        string? fileExtensionHint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        long? originalPos = source.CanSeek ? source.Position : null;

        try
        {
            var nbtDoc = await _nbtReader.ReadAsync(source, NbtReadOptions.Default, cancellationToken).ConfigureAwait(false);
            var formatId = DetectFromDocument(nbtDoc, out int version);

            if (formatId == SchematicFormatId.Unknown)
            {
                return new FormatDetectionResult(
                    IsRecognized: false,
                    FormatType: ModelFormatType.Unknown,
                    FormatId: "unknown",
                    Version: version,
                    Details: version != 0
                        ? $"Unsupported Sponge Schematic version {version}."
                        : "NBT document does not match Sponge v2, Sponge v3, or Legacy Schematic structure.");
            }

            return new FormatDetectionResult(
                IsRecognized: true,
                FormatType: ModelFormatType.Schematic,
                FormatId: formatId.ToString(),
                Version: version,
                Details: $"Detected {formatId} (version {version})");
        }
        catch (Exception ex)
        {
            return new FormatDetectionResult(
                IsRecognized: false,
                FormatType: ModelFormatType.Unknown,
                FormatId: "unknown",
                Version: 0,
                Details: ex.Message);
        }
        finally
        {
            if (originalPos.HasValue && source.CanSeek)
            {
                source.Position = originalPos.Value;
            }
        }
    }

    public async Task<SchematicFormatId> DetectSchematicFormatAsync(
        Stream source,
        NbtReadOptions? nbtOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        long? originalPos = source.CanSeek ? source.Position : null;

        try
        {
            var nbtDoc = await _nbtReader.ReadAsync(source, nbtOptions ?? NbtReadOptions.Default, cancellationToken).ConfigureAwait(false);
            var format = DetectFromDocument(nbtDoc, out int version);

            if (format == SchematicFormatId.Unknown && version != 0)
            {
                throw new SchematicException(
                    SchematicErrorCodes.UnsupportedVersion,
                    $"Sponge Schematic Version {version} is not supported (only Version 2 and Version 3 are supported).");
            }

            return format;
        }
        finally
        {
            if (originalPos.HasValue && source.CanSeek)
            {
                source.Position = originalPos.Value;
            }
        }
    }

    public SchematicFormatId DetectFromDocument(NbtDocument document, out int version)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (SpongeSchematicDetector.TryDetect(document, out var spongeFormat, out _, out version))
        {
            return spongeFormat;
        }

        if (version != 0)
        {
            // Document had a Sponge "Version" tag, but not Version 2 or 3
            return SchematicFormatId.Unknown;
        }

        if (LegacySchematicDetector.TryDetect(document, out _))
        {
            version = 1;
            return SchematicFormatId.LegacyMcEdit;
        }

        return SchematicFormatId.Unknown;
    }
}

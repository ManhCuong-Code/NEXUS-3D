using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// High-level reader for Sponge Schematic v2 and v3 streams/files.
/// </summary>
public sealed class SpongeSchematicReader
{
    private readonly INbtReader _nbtReader;
    private readonly ISpongeV2Parser _v2Parser;
    private readonly ISpongeV3Parser _v3Parser;

    public SpongeSchematicReader(
        INbtReader? nbtReader = null,
        ISpongeV2Parser? v2Parser = null,
        ISpongeV3Parser? v3Parser = null)
    {
        _nbtReader = nbtReader ?? new NbtReader();
        _v2Parser = v2Parser ?? new SpongeV2Parser();
        _v3Parser = v3Parser ?? new SpongeV3Parser();
    }

    public async Task<SchematicDocument> ReadAsync(
        Stream stream,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var effectiveOptions = options ?? SchematicReadOptions.Default;

        var nbtDoc = await _nbtReader.ReadAsync(stream, effectiveOptions.NbtOptions, cancellationToken).ConfigureAwait(false);
        return await ParseDocumentAsync(nbtDoc, effectiveOptions, cancellationToken).ConfigureAwait(false);
    }

    public Task<SchematicDocument> ParseDocumentAsync(
        NbtDocument nbtDocument,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nbtDocument);

        if (!SpongeSchematicDetector.TryDetect(nbtDocument, out var format, out _, out int version))
        {
            if (version != 0)
            {
                throw new SchematicException(
                    SchematicErrorCodes.UnsupportedVersion,
                    $"Sponge Schematic Version {version} is not supported (only Version 2 and Version 3 are supported).");
            }

            throw new SchematicException(
                SchematicErrorCodes.UnrecognizedSchematic,
                "The provided NBT document is not a valid Sponge v2 or v3 schematic.");
        }

        return format switch
        {
            SchematicFormatId.SpongeV2 => _v2Parser.ParseAsync(nbtDocument, options, cancellationToken),
            SchematicFormatId.SpongeV3 => _v3Parser.ParseAsync(nbtDocument, options, cancellationToken),
            _ => throw new SchematicException(
                SchematicErrorCodes.UnrecognizedSchematic,
                "The provided NBT document is not a valid Sponge v2 or v3 schematic.")
        };
    }
}

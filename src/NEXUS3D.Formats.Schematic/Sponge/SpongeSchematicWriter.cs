using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// High-level writer for Sponge Schematic v2 and v3 streams.
/// </summary>
public sealed class SpongeSchematicWriter
{
    private readonly INbtWriter _nbtWriter;
    private readonly ISpongeV2Exporter _v2Exporter;
    private readonly ISpongeV3Exporter _v3Exporter;

    public SpongeSchematicWriter(
        INbtWriter? nbtWriter = null,
        ISpongeV2Exporter? v2Exporter = null,
        ISpongeV3Exporter? v3Exporter = null)
    {
        _nbtWriter = nbtWriter ?? new NbtWriter();
        _v2Exporter = v2Exporter ?? new SpongeV2Exporter();
        _v3Exporter = v3Exporter ?? new SpongeV3Exporter();
    }

    public async Task WriteAsync(
        SchematicDocument document,
        Stream output,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);

        var effectiveOptions = options ?? SchematicWriteOptions.Default;

        NbtDocument nbtDoc = effectiveOptions.TargetFormat switch
        {
            SchematicFormatId.SpongeV2 => await _v2Exporter.ExportAsync(document, effectiveOptions, cancellationToken).ConfigureAwait(false),
            SchematicFormatId.SpongeV3 => await _v3Exporter.ExportAsync(document, effectiveOptions, cancellationToken).ConfigureAwait(false),
            _ => throw new SchematicException(
                SchematicErrorCodes.ExportFailed,
                $"SpongeSchematicWriter does not support target format '{effectiveOptions.TargetFormat}'.")
        };

        var nbtWriteOptions = new NbtWriteOptions
        {
            Compression = effectiveOptions.Compression,
            Endianness = NbtEndianness.BigEndian
        };

        await _nbtWriter.WriteAsync(nbtDoc, output, nbtWriteOptions, cancellationToken).ConfigureAwait(false);
    }
}

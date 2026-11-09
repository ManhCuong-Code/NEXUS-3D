using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Validation;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// Contract for exporting a <see cref="SchematicDocument"/> into a Sponge Schematic Version 2 <see cref="NbtDocument"/>.
/// </summary>
public interface ISpongeV2Exporter
{
    Task<NbtDocument> ExportAsync(
        SchematicDocument document,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds a Sponge Schematic Version 2 (<c>Version = 2</c>) NBT document.
/// </summary>
public sealed class SpongeV2Exporter : ISpongeV2Exporter
{
    private readonly PaletteEncoder _paletteEncoder = new();
    private readonly SpongeBlockDataEncoder _blockDataEncoder = new();
    private readonly BlockEntityWriter _blockEntityWriter = new();
    private readonly SpongeMetadataParser _metadataParser = new();
    private readonly SchematicValidator _validator = new();

    public Task<NbtDocument> ExportAsync(
        SchematicDocument document,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? SchematicWriteOptions.Default;

        var validation = _validator.Validate(document);
        if (!validation.IsValid)
        {
            var firstErr = validation.Issues.First();
            throw new SchematicException(firstErr.Code, firstErr.Message);
        }

        var dims = document.Dimensions;
        if (dims.Width > ushort.MaxValue || dims.Height > ushort.MaxValue || dims.Length > ushort.MaxValue)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidDimensions,
                $"Sponge v2 dimensions ({dims.Width}x{dims.Height}x{dims.Length}) exceed 16-bit unsigned integer limit (65535).");
        }

        var root = new NbtCompoundTag("Schematic");
        int dataVersion = document.DataVersion > 0 ? document.DataVersion : effectiveOptions.DataVersion;
        SchematicOffset effectiveOffset = effectiveOptions.OverrideOffset ?? document.Offset;

        root.PutInt("Version", 2);
        root.PutInt("DataVersion", dataVersion);
        root.PutShort("Width", unchecked((short)dims.Width));
        root.PutShort("Height", unchecked((short)dims.Height));
        root.PutShort("Length", unchecked((short)dims.Length));
        root.PutIntArray("Offset", effectiveOffset.ToIntArray());

        NbtCompoundTag paletteTag = _paletteEncoder.EncodePaletteCompound(document.Palette, "Palette");
        root.PutInt("PaletteMax", Math.Max(1, document.Palette.MaxIndex + 1));
        root.SetTag(paletteTag);

        byte[] blockData = _blockDataEncoder.Encode(document.BlockIndices, document.Palette);
        root.PutByteArray("BlockData", blockData);

        NbtListTag blockEntitiesTag = _blockEntityWriter.WriteSpongeV2BlockEntities(
            document.BlockEntities,
            dims,
            "BlockEntities");
        root.SetTag(blockEntitiesTag);

        var metadataTag = _metadataParser.Write(document.Metadata);
        if (metadataTag is not null)
        {
            root.SetTag(metadataTag);
        }

        if (effectiveOptions.PreserveUnknownData)
        {
            if (document.PreservedEntitiesTag is not null)
            {
                var entitiesClone = (NbtListTag)document.PreservedEntitiesTag.DeepClone();
                entitiesClone.Name = "Entities";
                root.SetTag(entitiesClone);
            }

            foreach (var kvp in document.UnmappedRootTags)
            {
                if (!root.Contains(kvp.Key))
                {
                    root.SetTag(kvp.Value.DeepClone());
                }
            }
        }

        var nbtDoc = new NbtDocument(
            root,
            effectiveOptions.Compression,
            NbtEndianness.BigEndian);

        return Task.FromResult(nbtDoc);
    }
}

using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Validation;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// Contract for exporting a <see cref="SchematicDocument"/> into a Sponge Schematic Version 3 <see cref="NbtDocument"/>.
/// </summary>
public interface ISpongeV3Exporter
{
    Task<NbtDocument> ExportAsync(
        SchematicDocument document,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds a Sponge Schematic Version 3 (<c>Version = 3</c>) NBT document with the canonical <c>Schematic</c>
/// wrapper compound and nested <c>Blocks</c> (<c>Palette</c>, <c>Data</c>, <c>BlockEntities</c>).
/// </summary>
public sealed class SpongeV3Exporter : ISpongeV3Exporter
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
                $"Sponge v3 dimensions ({dims.Width}x{dims.Height}x{dims.Length}) exceed 16-bit unsigned integer limit (65535).");
        }

        var schematicComp = new NbtCompoundTag("Schematic");
        int dataVersion = document.DataVersion > 0 ? document.DataVersion : effectiveOptions.DataVersion;
        SchematicOffset effectiveOffset = effectiveOptions.OverrideOffset ?? document.Offset;

        schematicComp.PutInt("Version", 3);
        schematicComp.PutInt("DataVersion", dataVersion);
        schematicComp.PutShort("Width", unchecked((short)dims.Width));
        schematicComp.PutShort("Height", unchecked((short)dims.Height));
        schematicComp.PutShort("Length", unchecked((short)dims.Length));
        schematicComp.PutIntArray("Offset", effectiveOffset.ToIntArray());

        var metadataTag = _metadataParser.Write(document.Metadata);
        if (metadataTag is not null)
        {
            schematicComp.SetTag(metadataTag);
        }

        var blocksComp = new NbtCompoundTag("Blocks");
        NbtCompoundTag paletteTag = _paletteEncoder.EncodePaletteCompound(document.Palette, "Palette");
        blocksComp.SetTag(paletteTag);

        byte[] blockData = _blockDataEncoder.Encode(document.BlockIndices, document.Palette);
        blocksComp.PutByteArray("Data", blockData);

        NbtListTag blockEntitiesTag = _blockEntityWriter.WriteSpongeV3BlockEntities(
            document.BlockEntities,
            dims,
            "BlockEntities");
        blocksComp.SetTag(blockEntitiesTag);

        schematicComp.SetTag(blocksComp);

        if (effectiveOptions.PreserveUnknownData)
        {
            if (document.PreservedBiomesTag is not null)
            {
                var biomesClone = document.PreservedBiomesTag.CloneCompound();
                biomesClone.Name = "Biomes";
                schematicComp.SetTag(biomesClone);
            }

            if (document.PreservedEntitiesTag is not null)
            {
                var entitiesClone = (NbtListTag)document.PreservedEntitiesTag.DeepClone();
                entitiesClone.Name = "Entities";
                schematicComp.SetTag(entitiesClone);
            }

            foreach (var kvp in document.UnmappedRootTags)
            {
                if (!schematicComp.Contains(kvp.Key))
                {
                    schematicComp.SetTag(kvp.Value.DeepClone());
                }
            }
        }

        // Sponge v3 wraps "Schematic" inside an unnamed root compound
        var outerRoot = new NbtCompoundTag(string.Empty);
        outerRoot.SetTag(schematicComp);

        var nbtDoc = new NbtDocument(
            outerRoot,
            effectiveOptions.Compression,
            NbtEndianness.BigEndian);

        return Task.FromResult(nbtDoc);
    }
}

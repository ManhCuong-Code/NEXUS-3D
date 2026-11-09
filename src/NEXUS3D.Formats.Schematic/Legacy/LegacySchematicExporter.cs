using NEXUS3D.Domain.Metadata;
using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Validation;

namespace NEXUS3D.Formats.Schematic.Legacy;

/// <summary>
/// Contract for exporting a <see cref="SchematicDocument"/> into a Legacy MCEdit/WorldEdit <c>.schematic</c> <see cref="NbtDocument"/>.
/// </summary>
public interface ILegacySchematicExporter
{
    Task<NbtDocument> ExportAsync(
        SchematicDocument document,
        SchematicWriteOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Full exporter implementation for Legacy <c>.schematic</c> files, writing <c>Blocks</c>, <c>Data</c>,
/// <c>AddBlocks</c> (when any block ID &gt; 255), <c>TileEntities</c>, <c>Entities</c>, and WorldEdit offsets.
/// </summary>
public sealed class LegacySchematicExporter : ILegacySchematicExporter
{
    private readonly ILegacyBlockIdMapping _idMapping;
    private readonly LegacyBlockEncoder _blockEncoder = new();
    private readonly LegacyMetadataEncoder _metadataEncoder = new();
    private readonly BlockEntityWriter _blockEntityWriter = new();
    private readonly SchematicValidator _validator = new();

    public LegacySchematicExporter(ILegacyBlockIdMapping? idMapping = null)
    {
        _idMapping = idMapping ?? new LegacyBlockIdMapping();
    }

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
        if (dims.Width > short.MaxValue || dims.Height > short.MaxValue || dims.Length > short.MaxValue)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidDimensions,
                $"Legacy schematic dimensions ({dims.Width}x{dims.Height}x{dims.Length}) exceed signed 16-bit integer limit (32767).");
        }

        int totalBlocks = dims.GetCheckedVolume();
        int[] blockIds = new int[totalBlocks];
        byte[] metadataBytes = new byte[totalBlocks];

        // Resolve palette index -> (blockId, metadata) once per palette entry
        var paletteToLegacy = new Dictionary<int, (int Id, byte Meta)>();
        foreach (var entry in document.Palette.EntriesByIndex.Values)
        {
            if (entry.LegacyBlockId is >= 0 and <= 4095 && entry.LegacyMetadata is >= 0 and <= 15)
            {
                paletteToLegacy[entry.Index] = (entry.LegacyBlockId.Value, (byte)entry.LegacyMetadata.Value);
            }
            else if (_idMapping.TryMapToLegacy(entry.BlockState, out int legId, out byte legMeta))
            {
                paletteToLegacy[entry.Index] = (legId, legMeta);
            }
            else if (effectiveOptions.StrictMapping)
            {
                throw new SchematicException(
                    SchematicErrorCodes.UnmappedLegacyId,
                    $"BlockState '{entry.RawStateString}' cannot be mapped to a Legacy numeric ID under StrictMapping.");
            }
            else
            {
                // Fallback to Stone (1:0) and record warning on document
                paletteToLegacy[entry.Index] = (1, 0);
                document.Warnings.Add(new WarningRecord
                {
                    Code = SchematicErrorCodes.UnmappedLegacyId,
                    Message = $"Modern BlockState '{entry.RawStateString}' has no Legacy 1.12.2 equivalent; approximated as Stone (1:0).",
                    SourceComponent = "LegacySchematicExporter"
                });
            }
        }

        for (int i = 0; i < totalBlocks; i++)
        {
            int paletteIdx = document.BlockIndices[i];
            if (!paletteToLegacy.TryGetValue(paletteIdx, out var pair))
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidPalette,
                    $"Block at linear index {i} references undefined palette index {paletteIdx}.");
            }

            blockIds[i] = pair.Id;
            metadataBytes[i] = pair.Meta;
        }

        var (blocksArray, addBlocksArray) = _blockEncoder.EncodeBlocks(blockIds);
        byte[] encodedData = _metadataEncoder.EncodeMetadata(metadataBytes);

        var root = new NbtCompoundTag("Schematic");
        root.PutShort("Width", (short)dims.Width);
        root.PutShort("Height", (short)dims.Height);
        root.PutShort("Length", (short)dims.Length);
        root.PutString("Materials", string.IsNullOrWhiteSpace(document.Metadata.LegacyMaterialsType) ? "Alpha" : document.Metadata.LegacyMaterialsType);

        SchematicOffset effectiveOffset = effectiveOptions.OverrideOffset ?? document.Metadata.WorldEditOffset ?? document.Offset;
        root.PutInt("WEOffsetX", effectiveOffset.X);
        root.PutInt("WEOffsetY", effectiveOffset.Y);
        root.PutInt("WEOffsetZ", effectiveOffset.Z);

        SchematicOffset origin = document.Metadata.WorldEditOrigin ?? SchematicOffset.Zero;
        root.PutInt("WEOriginX", origin.X);
        root.PutInt("WEOriginY", origin.Y);
        root.PutInt("WEOriginZ", origin.Z);

        root.PutByteArray("Blocks", blocksArray);
        root.PutByteArray("Data", encodedData);

        if (addBlocksArray is not null)
        {
            root.PutByteArray("AddBlocks", addBlocksArray);
        }

        NbtListTag tileEntities = _blockEntityWriter.WriteLegacyTileEntities(
            document.BlockEntities,
            dims,
            "TileEntities");
        root.SetTag(tileEntities);

        if (effectiveOptions.PreserveUnknownData && document.PreservedEntitiesTag is not null)
        {
            var entitiesClone = (NbtListTag)document.PreservedEntitiesTag.DeepClone();
            entitiesClone.Name = "Entities";
            root.SetTag(entitiesClone);
        }
        else
        {
            root.SetTag(new NbtListTag("Entities", NbtTagType.Compound));
        }

        if (effectiveOptions.PreserveUnknownData)
        {
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

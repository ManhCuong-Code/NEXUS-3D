using NEXUS3D.Domain.Metadata;
using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Validation;

namespace NEXUS3D.Formats.Schematic.Legacy;

/// <summary>
/// Contract for parsing Legacy MCEdit/WorldEdit <c>.schematic</c> documents.
/// </summary>
public interface ILegacySchematicParser
{
    Task<SchematicDocument> ParseAsync(
        NbtDocument document,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Full parser implementation for Legacy <c>.schematic</c> files (Alpha / 1.12.2), supporting
/// <c>Blocks</c>, <c>Data</c> (4-bit metadata), <c>AddBlocks</c> (packed high 4-bit nibbles for IDs 256..4095),
/// <c>TileEntities</c>, <c>Entities</c>, and WorldEdit offset/origin tags.
/// </summary>
public sealed class LegacySchematicParser : ILegacySchematicParser
{
    private readonly LegacyBlockDecoder _blockDecoder = new();
    private readonly LegacyMetadataDecoder _metadataDecoder = new();
    private readonly ILegacyBlockIdMapping _idMapping;
    private readonly BlockEntityParser _blockEntityParser = new();
    private readonly SchematicValidator _validator = new();

    public LegacySchematicParser(ILegacyBlockIdMapping? idMapping = null)
    {
        _idMapping = idMapping ?? new LegacyBlockIdMapping();
    }

    public Task<SchematicDocument> ParseAsync(
        NbtDocument document,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? SchematicReadOptions.Default;
        NbtCompoundTag root = LegacySchematicDetector.ResolveCandidateCompound(document.RootTag);

        // 1. Validate dimensions
        SchematicDimensions dimensions = LegacyFormatValidator.ReadAndValidateDimensions(root, effectiveOptions);
        int totalBlocks = dimensions.GetCheckedVolume(effectiveOptions.MaxTotalBlocks);

        // 2. Validate Materials (usually "Alpha")
        var warnings = new List<WarningRecord>();
        string materials = "Alpha";
        if (root.TryGetString("Materials", out string matStr))
        {
            materials = matStr;
            if (!string.Equals(materials, "Alpha", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(new WarningRecord
                {
                    Code = SchematicErrorCodes.UnsupportedVersion,
                    Message = $"Legacy schematic 'Materials' is '{materials}' rather than standard 'Alpha'.",
                    SourceComponent = "LegacySchematicParser"
                });
            }
        }

        // 3. Read Blocks and optional AddBlocks
        if (!root.TryGetByteArray("Blocks", out byte[] blocksBytes))
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Legacy schematic is missing required 'Blocks' TAG_Byte_Array.");
        }

        byte[] addBlocksBytes = [];
        if (root.Contains("AddBlocks"))
        {
            if (!root.TryGetByteArray("AddBlocks", out addBlocksBytes))
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidAddBlocks,
                    "Legacy schematic 'AddBlocks' tag must be TAG_Byte_Array.");
            }
        }
        else if (root.TryGetByteArray("Add", out byte[] addExpandedBytes))
        {
            // WorldEdit legacy "Add" array (1 byte per block) -> pack into nibble array
            if (addExpandedBytes.Length != totalBlocks)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidAddBlocks,
                    $"Legacy 'Add' array length ({addExpandedBytes.Length}) does not match volume ({totalBlocks}).");
            }

            addBlocksBytes = new LegacyMetadataEncoder().EncodePackedNibbles(addExpandedBytes);
        }

        int[] blockIds = _blockDecoder.DecodeBlocks(blocksBytes, addBlocksBytes, totalBlocks);

        // 4. Read Data (4-bit metadata)
        if (!root.TryGetByteArray("Data", out byte[] dataBytes))
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Legacy schematic is missing required 'Data' TAG_Byte_Array.");
        }

        byte[] metadataNibbles = _metadataDecoder.DecodeMetadata(dataBytes, totalBlocks);

        // 5. Build Palette and BlockIndices from (blockId, metadata) pairs
        var palette = new SchematicPalette();
        int[] blockIndices = new int[totalBlocks];
        var pairToPaletteIndex = new Dictionary<(int Id, byte Meta), int>();
        var warnedUnmappedPairs = new HashSet<(int Id, byte Meta)>();

        for (int i = 0; i < totalBlocks; i++)
        {
            var key = (blockIds[i], metadataNibbles[i]);
            if (!pairToPaletteIndex.TryGetValue(key, out int paletteIdx))
            {
                bool mapped = _idMapping.TryMapToBlockState(key.Item1, key.Item2, out var blockState, out bool isFallback);
                if (!mapped || isFallback)
                {
                    if (effectiveOptions.StrictLegacyMapping)
                    {
                        throw new SchematicException(
                            SchematicErrorCodes.UnmappedLegacyId,
                            $"Legacy Block ID {key.Item1}:{key.Item2} has no modern BlockState mapping under StrictLegacyMapping.");
                    }

                    if (warnedUnmappedPairs.Add(key))
                    {
                        warnings.Add(new WarningRecord
                        {
                            Code = SchematicErrorCodes.UnmappedLegacyId,
                            Message = $"Legacy Block ID {key.Item1} (metadata {key.Item2}) is unmapped; preserved losslessly as '{blockState}'.",
                            SourceComponent = "LegacySchematicParser"
                        });
                    }
                }

                paletteIdx = palette.GetOrAdd(blockState, key.Item1, key.Item2);
                pairToPaletteIndex[key] = paletteIdx;
            }

            blockIndices[i] = paletteIdx;
        }

        // 6. Read WorldEdit Offset & Origin
        var metadata = new SchematicMetadata { LegacyMaterialsType = materials };
        var offset = SchematicOffset.Zero;

        if (root.TryGetNumericAsInt("WEOffsetX", out int weOffX) &&
            root.TryGetNumericAsInt("WEOffsetY", out int weOffY) &&
            root.TryGetNumericAsInt("WEOffsetZ", out int weOffZ))
        {
            offset = new SchematicOffset(weOffX, weOffY, weOffZ);
            metadata.WorldEditOffset = offset;
        }

        if (root.TryGetNumericAsInt("WEOriginX", out int weOrigX) &&
            root.TryGetNumericAsInt("WEOriginY", out int weOrigY) &&
            root.TryGetNumericAsInt("WEOriginZ", out int weOrigZ))
        {
            metadata.WorldEditOrigin = new SchematicOffset(weOrigX, weOrigY, weOrigZ);
        }

        // 7. Read TileEntities and Entities
        root.TryGetList("TileEntities", out var tileEntitiesTag);
        var blockEntities = _blockEntityParser.ParseLegacyTileEntities(tileEntitiesTag);

        root.TryGetList("Entities", out var entitiesTag);

        var schemDoc = new SchematicDocument
        {
            Format = SchematicFormatId.LegacyMcEdit,
            FormatVersion = 1,
            DataVersion = 1343, // Minecraft 1.12.2 DataVersion
            Dimensions = dimensions,
            Offset = offset,
            Metadata = metadata,
            Palette = palette,
            BlockIndices = blockIndices,
            BlockEntities = blockEntities,
            PreservedEntitiesTag = entitiesTag?.DeepClone() as NbtListTag,
            Warnings = warnings
        };

        if (effectiveOptions.PreserveUnknownData)
        {
            foreach (var tag in root.GetAllTags())
            {
                if (tag.Name is not ("Width" or "Height" or "Length" or "Materials" or
                                     "Blocks" or "Data" or "AddBlocks" or "Add" or
                                     "WEOffsetX" or "WEOffsetY" or "WEOffsetZ" or
                                     "WEOriginX" or "WEOriginY" or "WEOriginZ" or
                                     "TileEntities" or "Entities"))
                {
                    schemDoc.UnmappedRootTags[tag.Name] = tag.DeepClone();
                }
            }
        }

        var validation = _validator.Validate(schemDoc);
        if (!validation.IsValid)
        {
            var firstErr = validation.Issues.First();
            throw new SchematicException(firstErr.Code, firstErr.Message);
        }

        return Task.FromResult(schemDoc);
    }
}

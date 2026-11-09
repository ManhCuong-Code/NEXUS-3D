using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Validation;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// Contract for parsing Sponge Schematic Version 2 (<c>.schem</c>) documents.
/// </summary>
public interface ISpongeV2Parser
{
    Task<SchematicDocument> ParseAsync(
        NbtDocument document,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Full parser implementation for Sponge Schematic Version 2.
/// </summary>
public sealed class SpongeV2Parser : ISpongeV2Parser
{
    private readonly PaletteDecoder _paletteDecoder = new();
    private readonly SpongeBlockDataDecoder _blockDataDecoder = new();
    private readonly BlockEntityParser _blockEntityParser = new();
    private readonly SpongeMetadataParser _metadataParser = new();
    private readonly SchematicValidator _validator = new();

    public Task<SchematicDocument> ParseAsync(
        NbtDocument document,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? SchematicReadOptions.Default;
        NbtCompoundTag root = SpongeSchematicDetector.ResolveCandidateCompound(document.RootTag);

        // 1. Check Version = 2
        if (!root.TryGetInt("Version", out int version))
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Sponge v2 schematic is missing required 'Version' TAG_Int.");
        }

        if (version != 2)
        {
            throw new SchematicException(
                SchematicErrorCodes.UnsupportedVersion,
                $"Expected Sponge Schematic Version 2, but found Version {version}.");
        }

        // 2. Read DataVersion
        if (!root.TryGetInt("DataVersion", out int dataVersion) || dataVersion <= 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Sponge v2 schematic is missing valid 'DataVersion' TAG_Int.");
        }

        // 3 & 4. Read and validate Width, Height, Length
        SchematicDimensions dimensions = SpongeFormatValidator.ReadAndValidateDimensions(root, effectiveOptions);

        // 5. Read Offset
        SchematicOffset offset = SchematicOffset.Zero;
        if (root.Contains("Offset"))
        {
            if (!root.TryGetIntArray("Offset", out int[] offsetArr))
            {
                throw new SchematicException(SchematicErrorCodes.InvalidOffset, "'Offset' tag must be TAG_Int_Array.");
            }
            offset = SchematicOffset.FromIntArray(offsetArr);
        }

        // 6. Read Metadata
        root.TryGetCompound("Metadata", out var metaComp);
        SchematicMetadata metadata = _metadataParser.Parse(metaComp);

        // 7 & 8. Read Palette and verify PaletteMax
        if (!root.TryGetCompound("Palette", out var paletteComp) || paletteComp is null)
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Sponge v2 schematic is missing required 'Palette' TAG_Compound.");
        }

        SchematicPalette palette = _paletteDecoder.DecodePaletteCompound(paletteComp);

        if (root.TryGetInt("PaletteMax", out int paletteMax))
        {
            if (paletteMax <= 0 || palette.MaxIndex >= paletteMax)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidPalette,
                    $"Sponge v2 PaletteMax ({paletteMax}) is invalid or smaller than max palette index ({palette.MaxIndex}).");
            }
        }

        // 9. Decode BlockData
        if (!root.TryGetByteArray("BlockData", out byte[] blockDataBytes))
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Sponge v2 schematic is missing required 'BlockData' TAG_Byte_Array.");
        }

        int[] blockIndices = _blockDataDecoder.Decode(blockDataBytes, dimensions, palette);

        // 10. Read BlockEntities (or legacy alias TileEntities in Sponge v2)
        NbtListTag? blockEntitiesTag = null;
        if (!root.TryGetList("BlockEntities", out blockEntitiesTag))
        {
            root.TryGetList("TileEntities", out blockEntitiesTag);
        }

        var blockEntities = _blockEntityParser.ParseSpongeV2BlockEntities(blockEntitiesTag);

        // Preserve Biomes / Entities / Unmapped root tags if present
        root.TryGetList("Entities", out var entitiesTag);

        var schemDoc = new SchematicDocument
        {
            Format = SchematicFormatId.SpongeV2,
            FormatVersion = 2,
            DataVersion = dataVersion,
            Dimensions = dimensions,
            Offset = offset,
            Metadata = metadata,
            Palette = palette,
            BlockIndices = blockIndices,
            BlockEntities = blockEntities,
            PreservedEntitiesTag = entitiesTag?.DeepClone() as NbtListTag
        };

        if (effectiveOptions.PreserveUnknownData)
        {
            foreach (var tag in root.GetAllTags())
            {
                if (tag.Name is not ("Version" or "DataVersion" or "Width" or "Height" or "Length" or
                                     "Offset" or "Metadata" or "Palette" or "PaletteMax" or "BlockData" or
                                     "BlockEntities" or "TileEntities" or "Entities"))
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

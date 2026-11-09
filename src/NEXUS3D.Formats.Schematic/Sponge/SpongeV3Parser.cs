using NEXUS3D.Domain.Metadata;
using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Validation;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// Contract for parsing Sponge Schematic Version 3 (<c>.schem</c>) documents.
/// </summary>
public interface ISpongeV3Parser
{
    Task<SchematicDocument> ParseAsync(
        NbtDocument document,
        SchematicReadOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Full parser implementation for Sponge Schematic Version 3.
/// Supports the nested <c>Schematic</c> root compound and <c>Blocks</c> (<c>Palette</c>, <c>Data</c>, <c>BlockEntities</c>),
/// while preserving <c>Biomes</c>, <c>Entities</c>, and unknown root tags.
/// </summary>
public sealed class SpongeV3Parser : ISpongeV3Parser
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
        NbtCompoundTag schematicComp = SpongeSchematicDetector.ResolveCandidateCompound(document.RootTag);

        // 1. Validate Version = 3
        if (!schematicComp.TryGetInt("Version", out int version))
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Sponge v3 schematic is missing required 'Version' TAG_Int.");
        }

        if (version != 3)
        {
            throw new SchematicException(
                SchematicErrorCodes.UnsupportedVersion,
                $"Expected Sponge Schematic Version 3, but found Version {version}.");
        }

        // 2. Read DataVersion
        if (!schematicComp.TryGetInt("DataVersion", out int dataVersion) || dataVersion <= 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Sponge v3 schematic is missing valid 'DataVersion' TAG_Int.");
        }

        // 3. Read and validate Width, Height, Length
        SchematicDimensions dimensions = SpongeFormatValidator.ReadAndValidateDimensions(schematicComp, effectiveOptions);

        // 4. Read Offset
        SchematicOffset offset = SchematicOffset.Zero;
        if (schematicComp.Contains("Offset"))
        {
            if (!schematicComp.TryGetIntArray("Offset", out int[] offsetArr))
            {
                throw new SchematicException(SchematicErrorCodes.InvalidOffset, "'Offset' tag must be TAG_Int_Array.");
            }
            offset = SchematicOffset.FromIntArray(offsetArr);
        }

        // 5. Read Metadata
        schematicComp.TryGetCompound("Metadata", out var metaComp);
        SchematicMetadata metadata = _metadataParser.Parse(metaComp);

        // 6. Read Blocks compound
        if (!schematicComp.TryGetCompound("Blocks", out var blocksComp) || blocksComp is null)
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Sponge v3 schematic is missing required 'Blocks' TAG_Compound.");
        }

        if (!blocksComp.TryGetCompound("Palette", out var paletteComp) || paletteComp is null)
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Sponge v3 schematic 'Blocks' compound is missing required 'Palette' TAG_Compound.");
        }

        SchematicPalette palette = _paletteDecoder.DecodePaletteCompound(paletteComp);

        if (!blocksComp.TryGetByteArray("Data", out byte[] blockDataBytes))
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Sponge v3 schematic 'Blocks' compound is missing required 'Data' TAG_Byte_Array.");
        }

        int[] blockIndices = _blockDataDecoder.Decode(blockDataBytes, dimensions, palette);

        blocksComp.TryGetList("BlockEntities", out var blockEntitiesTag);
        var blockEntities = _blockEntityParser.ParseSpongeV3BlockEntities(blockEntitiesTag);

        // 7. Preserve Biomes and Entities with informative warnings
        var warnings = new List<WarningRecord>();

        NbtCompoundTag? preservedBiomes = null;
        if (schematicComp.TryGetCompound("Biomes", out var biomesComp) && biomesComp is not null)
        {
            preservedBiomes = biomesComp.CloneCompound();
            warnings.Add(new WarningRecord
            {
                Code = "SCH_WARN_BIOMES_PRESERVED",
                Message = "Sponge v3 'Biomes' data is preserved as raw NBT for round-trip export; USM does not natively edit biome layers.",
                SourceComponent = "SpongeV3Parser"
            });
        }

        NbtListTag? preservedEntities = null;
        if (schematicComp.TryGetList("Entities", out var entitiesList) && entitiesList is not null)
        {
            preservedEntities = entitiesList.DeepClone() as NbtListTag;
            warnings.Add(new WarningRecord
            {
                Code = "SCH_WARN_ENTITIES_PRESERVED",
                Message = $"Sponge v3 'Entities' list ({entitiesList.Count} entries) is preserved as raw NBT for round-trip export.",
                SourceComponent = "SpongeV3Parser"
            });
        }

        var schemDoc = new SchematicDocument
        {
            Format = SchematicFormatId.SpongeV3,
            FormatVersion = 3,
            DataVersion = dataVersion,
            Dimensions = dimensions,
            Offset = offset,
            Metadata = metadata,
            Palette = palette,
            BlockIndices = blockIndices,
            BlockEntities = blockEntities,
            PreservedBiomesTag = preservedBiomes,
            PreservedEntitiesTag = preservedEntities,
            Warnings = warnings
        };

        if (effectiveOptions.PreserveUnknownData)
        {
            foreach (var tag in schematicComp.GetAllTags())
            {
                if (tag.Name is not ("Version" or "DataVersion" or "Width" or "Height" or "Length" or
                                     "Offset" or "Metadata" or "Blocks" or "Biomes" or "Entities"))
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

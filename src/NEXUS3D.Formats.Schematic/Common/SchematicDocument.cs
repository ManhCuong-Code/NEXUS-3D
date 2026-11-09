using NEXUS3D.Domain.Metadata;
using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Palette;

namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Intermediate schematic document model holding parsed dimensions, offset, palette, linear YZX block indices,
/// block entities, metadata, and preserved Biomes/Entities/unmapped NBT tags.
/// </summary>
public sealed class SchematicDocument
{
    public int FormatVersion { get; set; } = 2;
    public int DataVersion { get; set; } = 3465; // Minecraft 1.20.1 default
    public SchematicFormatId Format { get; set; } = SchematicFormatId.SpongeV2;
    public SchematicDimensions Dimensions { get; set; } = new(1, 1, 1);
    public SchematicOffset Offset { get; set; } = SchematicOffset.Zero;
    public SchematicPalette Palette { get; set; } = new();
    public int[] BlockIndices { get; set; } = [];
    public List<SchematicBlockEntity> BlockEntities { get; set; } = [];
    public SchematicMetadata Metadata { get; set; } = new();
    public NbtCompoundTag? PreservedBiomesTag { get; set; }
    public NbtListTag? PreservedEntitiesTag { get; set; }
    public Dictionary<string, NbtTag> UnmappedRootTags { get; set; } = new(StringComparer.Ordinal);
    public RawDataContainer? RawData { get; set; }
    public List<WarningRecord> Warnings { get; set; } = [];

    public int GetPaletteIndexAt(int x, int y, int z)
    {
        int linearIdx = SchematicIndexing.ToIndex(x, y, z, Dimensions.Width, Dimensions.Length);
        return BlockIndices[linearIdx];
    }

    public void SetPaletteIndexAt(int x, int y, int z, int paletteIndex)
    {
        int linearIdx = SchematicIndexing.ToIndex(x, y, z, Dimensions.Width, Dimensions.Length);
        BlockIndices[linearIdx] = paletteIndex;
    }
}


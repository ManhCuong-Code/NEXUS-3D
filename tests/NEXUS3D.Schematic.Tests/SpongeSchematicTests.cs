using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.BlockStates;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Sponge;

namespace NEXUS3D.Schematic.Tests;

public sealed class SpongeSchematicTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(3, 3, 3)]
    [InlineData(16, 16, 16)]
    public async Task SpongeV2_ExportAndParse_PreservesDimensionsPaletteBlockEntitiesAndMetadata(
        int width, int height, int length)
    {
        var palette = new SchematicPalette();
        int airIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:air"));
        int stoneIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:stone"));
        int stairsIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:oak_stairs[facing=north,half=bottom,shape=straight,waterlogged=false]"));
        int chestIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:chest[facing=south,type=single,waterlogged=false]"));

        var dims = new SchematicDimensions(width, height, length);
        int total = dims.GetCheckedVolume();
        int[] indices = new int[total];

        for (int i = 0; i < total; i++)
        {
            indices[i] = (i % 4) switch
            {
                0 => airIdx,
                1 => stoneIdx,
                2 => stairsIdx,
                _ => chestIdx
            };
        }

        var chestData = new NbtCompoundTag("Data");
        chestData.PutString("CustomName", "{\"text\":\"Supply Crate\"}");
        var items = new NbtListTag("Items", NbtTagType.Compound);
        var slot0 = new NbtCompoundTag(string.Empty);
        slot0.PutByte("Slot", 0);
        slot0.PutString("id", "minecraft:diamond");
        slot0.PutByte("Count", 64);
        items.Add(slot0);
        chestData.SetTag(items);

        var doc = new SchematicDocument
        {
            Format = SchematicFormatId.SpongeV2,
            FormatVersion = 2,
            DataVersion = 3465,
            Dimensions = dims,
            Offset = new SchematicOffset(-5, 64, 12),
            Palette = palette,
            BlockIndices = indices,
            Metadata = new SchematicMetadata
            {
                Name = "TestTowerV2",
                Author = "NEXUS3D",
                DateUnixMilliseconds = 1700000000000L,
                RequiredMods = ["create", "worldedit"],
                WorldEditOffset = new SchematicOffset(-5, 64, 12)
            },
            BlockEntities =
            [
                new SchematicBlockEntity
                {
                    Id = "minecraft:chest",
                    Position = VoxelPosition.Zero,
                    Data = chestData,
                    SourceVersion = 2
                }
            ]
        };

        var exporter = new SpongeV2Exporter();
        var parser = new SpongeV2Parser();

        NbtDocument nbtDoc = await exporter.ExportAsync(doc);
        SchematicDocument parsed = await parser.ParseAsync(nbtDoc);

        Assert.Equal(SchematicFormatId.SpongeV2, parsed.Format);
        Assert.Equal(2, parsed.FormatVersion);
        Assert.Equal(3465, parsed.DataVersion);
        Assert.Equal(dims, parsed.Dimensions);
        Assert.Equal(new SchematicOffset(-5, 64, 12), parsed.Offset);
        Assert.Equal("TestTowerV2", parsed.Metadata.Name);
        Assert.Equal("NEXUS3D", parsed.Metadata.Author);
        Assert.Equal(1700000000000L, parsed.Metadata.DateUnixMilliseconds);
        Assert.Equal(["create", "worldedit"], parsed.Metadata.RequiredMods);
        Assert.Equal(indices, parsed.BlockIndices);

        Assert.Single(parsed.BlockEntities);
        Assert.Equal("minecraft:chest", parsed.BlockEntities[0].Id);
        Assert.Equal(VoxelPosition.Zero, parsed.BlockEntities[0].Position);
        Assert.True(parsed.BlockEntities[0].Data.TryGetString("CustomName", out string customName));
        Assert.Contains("Supply Crate", customName);
    }

    [Fact]
    public async Task SpongeV2_NonContiguousPaletteIndices_DecodesCorrectly()
    {
        var root = new NbtCompoundTag("Schematic");
        root.PutInt("Version", 2);
        root.PutInt("DataVersion", 3465);
        root.PutShort("Width", 2);
        root.PutShort("Height", 1);
        root.PutShort("Length", 1);

        var palette = new NbtCompoundTag("Palette");
        palette.PutInt("minecraft:air", 0);
        palette.PutInt("minecraft:diamond_block", 200); // > 127 (2-byte VarInt) and non-contiguous!
        root.SetTag(palette);
        root.PutInt("PaletteMax", 201);

        byte[] blockData = VarIntEncoder.EncodeArray([0, 200]);
        root.PutByteArray("BlockData", blockData);

        var parsed = await new SpongeV2Parser().ParseAsync(new NbtDocument(root));
        Assert.Equal(0, parsed.GetPaletteIndexAt(0, 0, 0));
        Assert.Equal(200, parsed.GetPaletteIndexAt(1, 0, 0));
        Assert.True(parsed.Palette.TryGetByIndex(200, out var entry) && entry!.BlockState.Name == "diamond_block");
    }

    [Fact]
    public async Task SpongeV3_ExportAndParse_PreservesNestedBlocksBiomesAndEntities()
    {
        var palette = new SchematicPalette();
        int airIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:air"));
        int lanternIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:sea_lantern"));

        var biomesTag = new NbtCompoundTag("Biomes");
        var biomePalette = new NbtCompoundTag("Palette");
        biomePalette.PutInt("minecraft:plains", 0);
        biomesTag.SetTag(biomePalette);
        biomesTag.PutByteArray("Data", [0, 0, 0, 0, 0, 0, 0, 0]);

        var entitiesTag = new NbtListTag("Entities", NbtTagType.Compound);
        var armorStand = new NbtCompoundTag(string.Empty);
        armorStand.PutString("Id", "minecraft:armor_stand");
        entitiesTag.Add(armorStand);

        var signData = new NbtCompoundTag("Data");
        signData.PutString("Text1", "{\"text\":\"NEXUS 3D v3\"}");

        var doc = new SchematicDocument
        {
            Format = SchematicFormatId.SpongeV3,
            FormatVersion = 3,
            DataVersion = 3465,
            Dimensions = new SchematicDimensions(2, 2, 2),
            Offset = new SchematicOffset(10, 20, 30),
            Palette = palette,
            BlockIndices = [airIdx, lanternIdx, lanternIdx, airIdx, lanternIdx, airIdx, airIdx, lanternIdx],
            PreservedBiomesTag = biomesTag,
            PreservedEntitiesTag = entitiesTag,
            BlockEntities =
            [
                new SchematicBlockEntity
                {
                    Id = "minecraft:sign",
                    Position = new VoxelPosition(1, 0, 0),
                    Data = signData,
                    SourceVersion = 3
                }
            ]
        };

        var exporter = new SpongeV3Exporter();
        var parser = new SpongeV3Parser();

        NbtDocument nbtDoc = await exporter.ExportAsync(doc);
        SchematicDocument parsed = await parser.ParseAsync(nbtDoc);

        Assert.Equal(SchematicFormatId.SpongeV3, parsed.Format);
        Assert.Equal(3, parsed.FormatVersion);
        Assert.Equal(new SchematicDimensions(2, 2, 2), parsed.Dimensions);
        Assert.Equal(doc.BlockIndices, parsed.BlockIndices);
        Assert.NotNull(parsed.PreservedBiomesTag);
        Assert.NotNull(parsed.PreservedEntitiesTag);
        Assert.Equal(1, parsed.PreservedEntitiesTag!.Count);
        Assert.Equal(2, parsed.Warnings.Count); // Biomes + Entities preservation warnings
        Assert.Single(parsed.BlockEntities);
        Assert.True(parsed.BlockEntities[0].Data.TryGetString("Text1", out string text1) && text1.Contains("NEXUS 3D v3"));
    }

    [Fact]
    public async Task SpongeV2_InvalidPaletteIndexInBlockData_ThrowsSch005()
    {
        var root = new NbtCompoundTag("Schematic");
        root.PutInt("Version", 2);
        root.PutInt("DataVersion", 3465);
        root.PutShort("Width", 1);
        root.PutShort("Height", 1);
        root.PutShort("Length", 1);

        var palette = new NbtCompoundTag("Palette");
        palette.PutInt("minecraft:stone", 0);
        root.SetTag(palette);
        root.PutByteArray("BlockData", VarIntEncoder.EncodeArray([99])); // Index 99 not in palette!

        var ex = await Assert.ThrowsAsync<SchematicException>(() =>
            new SpongeV2Parser().ParseAsync(new NbtDocument(root)));
        Assert.Equal(SchematicErrorCodes.InvalidPalette, ex.ErrorCode);
    }
}

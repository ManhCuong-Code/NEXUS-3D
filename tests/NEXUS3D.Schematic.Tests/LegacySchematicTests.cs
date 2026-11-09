using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Legacy;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Schematic.Tests;

public sealed class LegacySchematicTests
{
    [Fact]
    public void LegacyBlockEncoderAndDecoder_WithAddBlocks_HighBitNibbles_EvenAndOddIndices_RoundTripsExact12BitIds()
    {
        // Test IDs < 256 and IDs in 256..4095 at both even and odd linear indices
        int[] originalIds = [0, 1, 255, 256, 300, 1024, 2048, 4095, 42];
        var encoder = new LegacyBlockEncoder();
        var decoder = new LegacyBlockDecoder();

        var (blocks, addBlocks) = encoder.EncodeBlocks(originalIds);
        Assert.NotNull(addBlocks);
        Assert.Equal((originalIds.Length + 1) / 2, addBlocks!.Length);

        int[] decodedIds = decoder.DecodeBlocks(blocks, addBlocks, originalIds.Length);
        Assert.Equal(originalIds, decodedIds);
    }

    [Fact]
    public async Task LegacySchematic_ParseAndExport_WithAddBlocksAndTileEntities_RoundTripsLosslessly()
    {
        var root = new NbtCompoundTag("Schematic");
        root.PutShort("Width", 2);
        root.PutShort("Height", 1);
        root.PutShort("Length", 2);
        root.PutString("Materials", "Alpha");
        root.PutInt("WEOffsetX", -2);
        root.PutInt("WEOffsetY", 0);
        root.PutInt("WEOffsetZ", -2);
        root.PutInt("WEOriginX", 100);
        root.PutInt("WEOriginY", 64);
        root.PutInt("WEOriginZ", 200);

        // 4 blocks:
        // index 0: 1:0 (stone)
        // index 1: 35:14 (red_wool)
        // index 2: 54:2 (chest)
        // index 3: 750:5 (unmapped custom modded 12-bit block ID > 255!)
        int[] blockIds = [1, 35, 54, 750];
        byte[] meta = [0, 14, 2, 5];

        var (blocksBytes, addBlocksBytes) = new LegacyBlockEncoder().EncodeBlocks(blockIds);
        root.PutByteArray("Blocks", blocksBytes);
        root.PutByteArray("AddBlocks", addBlocksBytes!);
        root.PutByteArray("Data", meta);

        var tileEntities = new NbtListTag("TileEntities", NbtTagType.Compound);
        var chestTe = new NbtCompoundTag(string.Empty);
        chestTe.PutString("id", "Chest");
        chestTe.PutInt("x", 0);
        chestTe.PutInt("y", 0);
        chestTe.PutInt("z", 1); // index = 0 + 1 * 2 + 0 = 2
        chestTe.PutString("Lock", "SecretKey");
        tileEntities.Add(chestTe);
        root.SetTag(tileEntities);
        root.SetTag(new NbtListTag("Entities", NbtTagType.Compound));

        var parser = new LegacySchematicParser();
        var exporter = new LegacySchematicExporter();

        SchematicDocument parsed = await parser.ParseAsync(new NbtDocument(root));
        Assert.Equal(SchematicFormatId.LegacyMcEdit, parsed.Format);
        Assert.Equal(new SchematicDimensions(2, 1, 2), parsed.Dimensions);
        Assert.Equal(new SchematicOffset(-2, 0, -2), parsed.Offset);
        Assert.Equal(new SchematicOffset(100, 64, 200), parsed.Metadata.WorldEditOrigin);

        // Verify warning SCH009 was emitted for unmapped ID 750:5 and preserved as legacy:block_750[meta=5]
        Assert.Contains(parsed.Warnings, w => w.Code == SchematicErrorCodes.UnmappedLegacyId);
        Assert.True(parsed.Palette.TryGetByIndex(parsed.BlockIndices[3], out var unmappedEntry));
        Assert.Equal("legacy", unmappedEntry!.BlockState.Namespace);
        Assert.Equal("block_750", unmappedEntry.BlockState.Name);
        Assert.Equal("5", unmappedEntry.BlockState.Properties["meta"]);

        // Re-export to Legacy NBT and re-parse to verify 100% lossless round-trip of IDs 0..4095 & metadata
        NbtDocument exportedNbt = await exporter.ExportAsync(parsed, new SchematicWriteOptions
        {
            TargetFormat = SchematicFormatId.LegacyMcEdit,
            StrictMapping = true // Should succeed because legacy:block_750[meta=5] maps back to 750:5!
        });

        Assert.True(exportedNbt.RootTag.TryGetByteArray("Blocks", out byte[] reExportedBlocks));
        Assert.True(exportedNbt.RootTag.TryGetByteArray("AddBlocks", out byte[] reExportedAddBlocks));
        Assert.True(exportedNbt.RootTag.TryGetByteArray("Data", out byte[] reExportedData));

        Assert.Equal(blocksBytes, reExportedBlocks);
        Assert.Equal(addBlocksBytes, reExportedAddBlocks);
        Assert.Equal(meta, reExportedData);
    }

    [Fact]
    public void LegacyMetadataDecoder_ValueGreaterThan15_ThrowsSch010()
    {
        var decoder = new LegacyMetadataDecoder();
        var ex = Assert.Throws<SchematicException>(() => decoder.DecodeMetadata([0, 16], 2));
        Assert.Equal(SchematicErrorCodes.InvalidLegacyMetadata, ex.ErrorCode);
    }

    [Fact]
    public void LegacyBlockDecoder_InvalidAddBlocksLength_ThrowsSch011()
    {
        var decoder = new LegacyBlockDecoder();
        var ex = Assert.Throws<SchematicException>(() =>
            decoder.DecodeBlocks(new byte[8], new byte[3], 8)); // expected 4 bytes for 8 blocks
        Assert.Equal(SchematicErrorCodes.InvalidAddBlocks, ex.ErrorCode);
    }
}


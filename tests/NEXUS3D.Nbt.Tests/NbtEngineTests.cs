using NEXUS3D.Formats.Schematic.Nbt;
using NEXUS3D.Formats.Schematic.Nbt.Compression;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Validation;

namespace NEXUS3D.Nbt.Tests;

public sealed class NbtEngineTests
{
    [Theory]
    [InlineData(NbtCompressionType.None, NbtEndianness.BigEndian)]
    [InlineData(NbtCompressionType.GZip, NbtEndianness.BigEndian)]
    [InlineData(NbtCompressionType.ZLib, NbtEndianness.BigEndian)]
    [InlineData(NbtCompressionType.None, NbtEndianness.LittleEndian)]
    public async Task WriteAndRead_All13TagTypes_RoundTripsIdentically(
        NbtCompressionType compression,
        NbtEndianness endianness)
    {
        var root = new NbtCompoundTag("RootLevel");
        root.PutByte("ByteVal", -42);
        root.PutShort("ShortVal", 32000);
        root.PutInt("IntVal", 123456789);
        root.PutLong("LongVal", -987654321012345L);
        root.SetTag(new NbtFloatTag("FloatVal", 3.1415927f));
        root.SetTag(new NbtDoubleTag("DoubleVal", 2.718281828459045));
        root.PutByteArray("ByteArr", [0x01, 0x7F, 0x80, 0xFF]);
        root.PutString("StringVal", "Hello Minecraft Schematic \u2603!");
        root.PutIntArray("IntArr", [-100, 0, 100, int.MaxValue, int.MinValue]);
        root.SetTag(new NbtLongArrayTag("LongArr", [long.MinValue, -1L, 0L, 42L, long.MaxValue]));

        var list = new NbtListTag("CompoundList", NbtTagType.Compound);
        var item1 = new NbtCompoundTag(string.Empty);
        item1.PutString("id", "minecraft:diamond_sword");
        item1.PutByte("Count", 1);
        list.Add(item1);
        root.SetTag(list);

        var emptyList = new NbtListTag("EmptyList", NbtTagType.End);
        root.SetTag(emptyList);

        var emptyCompound = new NbtCompoundTag("EmptyCompound");
        root.SetTag(emptyCompound);

        var doc = new NbtDocument(root, compression, endianness);
        var writer = new NbtWriter();
        var reader = new NbtReader();

        using var ms = new MemoryStream();
        await writer.WriteAsync(doc, ms, new NbtWriteOptions
        {
            Compression = compression,
            Endianness = endianness
        });

        ms.Position = 0;
        var readDoc = await reader.ReadAsync(ms, new NbtReadOptions
        {
            Compression = NbtCompressionType.AutoDetect,
            Endianness = endianness
        });

        Assert.Equal("RootLevel", readDoc.RootTag.Name);
        Assert.True(readDoc.RootTag.TryGetByte("ByteVal", out sbyte b) && b == -42);
        Assert.True(readDoc.RootTag.TryGetShort("ShortVal", out short s) && s == 32000);
        Assert.True(readDoc.RootTag.TryGetInt("IntVal", out int i) && i == 123456789);
        Assert.True(readDoc.RootTag.TryGetLong("LongVal", out long l) && l == -987654321012345L);
        Assert.Equal(3.1415927f, readDoc.RootTag.GetTag<NbtFloatTag>("FloatVal")!.Value);
        Assert.Equal(2.718281828459045, readDoc.RootTag.GetTag<NbtDoubleTag>("DoubleVal")!.Value);
        Assert.Equal(new byte[] { 0x01, 0x7F, 0x80, 0xFF }, readDoc.RootTag.GetTag<NbtByteArrayTag>("ByteArr")!.Value);
        Assert.True(readDoc.RootTag.TryGetString("StringVal", out string str) && str == "Hello Minecraft Schematic \u2603!");
        Assert.Equal(new[] { -100, 0, 100, int.MaxValue, int.MinValue }, readDoc.RootTag.GetTag<NbtIntArrayTag>("IntArr")!.Value);
        Assert.Equal(new[] { long.MinValue, -1L, 0L, 42L, long.MaxValue }, readDoc.RootTag.GetTag<NbtLongArrayTag>("LongArr")!.Value);

        Assert.True(readDoc.RootTag.TryGetList("CompoundList", out var readList) && readList!.Count == 1);
        var readItem1 = Assert.IsType<NbtCompoundTag>(readList[0]);
        Assert.True(readItem1.TryGetString("id", out string itemId) && itemId == "minecraft:diamond_sword");

        Assert.True(readDoc.RootTag.TryGetList("EmptyList", out var readEmptyList) && readEmptyList!.Count == 0);
        Assert.True(readDoc.RootTag.TryGetCompound("EmptyCompound", out var readEmptyComp) && readEmptyComp!.Count == 0);

        var validation = new NbtValidator().Validate(readDoc);
        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task ReadAsync_CorruptedGZipStream_ThrowsNbt004()
    {
        // GZip magic 0x1F 0x8B followed by garbage bytes
        byte[] corruptedGzip = [0x1F, 0x8B, 0x08, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x12, 0x34];
        using var ms = new MemoryStream(corruptedGzip);
        var reader = new NbtReader();

        var ex = await Assert.ThrowsAsync<NbtException>(() => reader.ReadAsync(ms));
        Assert.Equal(NbtErrorCodes.CompressionError, ex.ErrorCode);
    }

    [Fact]
    public void ReadRootCompound_InvalidTagTypeId_ThrowsNbt001Or002()
    {
        // Root tag type = 99 (invalid)
        byte[] invalidTagBytes = [99, 0x00, 0x00];
        var ex = Assert.Throws<NbtException>(() =>
        {
            var reader = new NbtBinaryReader(invalidTagBytes);
            reader.ReadRootCompound();
        });
        Assert.True(ex.ErrorCode is NbtErrorCodes.UnsupportedTagType or NbtErrorCodes.InvalidNbt);
    }

    [Fact]
    public void ReadRootCompound_TruncatedStream_ThrowsNbt001()
    {
        // Compound (10), name length 4 ("Test"), Int tag (3), name length 1 ("x"), but only 2 bytes of int payload and no End tag
        byte[] truncated = [10, 0x00, 0x04, (byte)'T', (byte)'e', (byte)'s', (byte)'t', 3, 0x00, 0x01, (byte)'x', 0x00, 0x01];
        var ex = Assert.Throws<NbtException>(() =>
        {
            var reader = new NbtBinaryReader(truncated);
            reader.ReadRootCompound();
        });
        Assert.Equal(NbtErrorCodes.InvalidNbt, ex.ErrorCode);
    }

    [Fact]
    public async Task ReadAsync_NestingDepthExceedsLimit_ThrowsNbt003()
    {
        var root = new NbtCompoundTag("Root");
        NbtCompoundTag current = root;
        for (int i = 0; i < 20; i++)
        {
            var child = new NbtCompoundTag($"Level_{i}");
            current.SetTag(child);
            current = child;
        }

        using var ms = new MemoryStream();
        await new NbtWriter().WriteAsync(new NbtDocument(root), ms, new NbtWriteOptions { Compression = NbtCompressionType.None });

        ms.Position = 0;
        var reader = new NbtReader();
        var ex = await Assert.ThrowsAsync<NbtException>(() =>
            reader.ReadAsync(ms, new NbtReadOptions { MaxNestingDepth = 10 }));

        Assert.Equal(NbtErrorCodes.LimitExceeded, ex.ErrorCode);
    }

    [Fact]
    public async Task ReadAsync_DecompressedSizeExceedsLimit_ThrowsNbt003()
    {
        var root = new NbtCompoundTag("Root");
        root.PutByteArray("Payload", new byte[4096]);

        using var ms = new MemoryStream();
        await new NbtWriter().WriteAsync(new NbtDocument(root), ms, new NbtWriteOptions { Compression = NbtCompressionType.GZip });

        ms.Position = 0;
        var reader = new NbtReader();
        var ex = await Assert.ThrowsAsync<NbtException>(() =>
            reader.ReadAsync(ms, new NbtReadOptions { MaxDecompressedBytes = 512 }));

        Assert.Equal(NbtErrorCodes.LimitExceeded, ex.ErrorCode);
    }
}

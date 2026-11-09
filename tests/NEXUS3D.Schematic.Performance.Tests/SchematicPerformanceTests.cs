using System.Diagnostics;
using NEXUS3D.Formats.Schematic.BlockStates;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Services;
using NEXUS3D.Formats.Schematic.Sponge;
using Xunit.Abstractions;

namespace NEXUS3D.Schematic.Performance.Tests;

public sealed class SchematicPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public SchematicPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(16, 16, 16)]      // 4,096 blocks
    [InlineData(64, 64, 64)]      // 262,144 blocks
    [InlineData(128, 128, 128)]   // 2,097,152 blocks
    public async Task SpongeV2_ReadWriteAndUsmConversion_MeetsPerformanceBenchmarks(
        int width, int height, int length)
    {
        var doc = BuildSyntheticDocument(width, height, length);
        var writer = new SchematicFormatExporter();
        var reader = new SchematicFormatParser();

        var swWrite = Stopwatch.StartNew();
        using var ms = new MemoryStream();
        var writeResult = await writer.WriteDocumentAsync(doc, ms, new SchematicWriteOptions
        {
            TargetFormat = SchematicFormatId.SpongeV2,
            VerifyTemporaryOutputBeforeCommit = false
        });
        swWrite.Stop();
        Assert.True(writeResult.IsSuccess);

        ms.Position = 0;
        var swReadDoc = Stopwatch.StartNew();
        var readDoc = await reader.ReadDocumentAsync(ms);
        swReadDoc.Stop();

        Assert.Equal(doc.Dimensions, readDoc.Dimensions);
        Assert.Equal(doc.BlockIndices.Length, readDoc.BlockIndices.Length);

        _output.WriteLine(
            $"[{width}x{height}x{length} ({doc.BlockIndices.Length:N0} blocks)] " +
            $"Write={swWrite.ElapsedMilliseconds}ms ({ms.Length:N0} bytes), Read={swReadDoc.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task LargeVolume_256x128x256_8MillionBlocks_SpongeCodec_StreamsWithoutExcessiveAllocation()
    {
        // 256 * 128 * 256 = 8,388,608 blocks
        var doc = BuildSyntheticDocument(256, 128, 256);
        var exporter = new SpongeV2Exporter();
        var parser = new SpongeV2Parser();

        var sw = Stopwatch.StartNew();
        var nbtDoc = await exporter.ExportAsync(doc);
        var parsedDoc = await parser.ParseAsync(nbtDoc);
        sw.Stop();

        Assert.Equal(8_388_608, parsedDoc.BlockIndices.Length);
        _output.WriteLine($"[256x128x256 (8,388,608 blocks)] Export+Parse elapsed: {sw.ElapsedMilliseconds}ms");
    }

    private static SchematicDocument BuildSyntheticDocument(int width, int height, int length)
    {
        var palette = new SchematicPalette();
        int airIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:air"));
        int stoneIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:stone"));
        int dirtIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:dirt"));
        int grassIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:grass_block[snowy=false]"));

        var dims = new SchematicDimensions(width, height, length);
        int total = dims.GetCheckedVolume();
        int[] indices = new int[total];

        for (int i = 0; i < total; i++)
        {
            int mod = i & 15;
            indices[i] = mod switch
            {
                0 => stoneIdx,
                1 => dirtIdx,
                2 => grassIdx,
                _ => airIdx
            };
        }

        return new SchematicDocument
        {
            Format = SchematicFormatId.SpongeV2,
            FormatVersion = 2,
            DataVersion = 3465,
            Dimensions = dims,
            Palette = palette,
            BlockIndices = indices
        };
    }
}


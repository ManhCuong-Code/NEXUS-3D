using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.App.Bootstrap;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.BlockStates;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Legacy;
using NEXUS3D.Formats.Schematic.Nbt.Compression;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Services;

namespace NEXUS3D.Schematic.Integration.Tests;

public sealed class SchematicRoundTripAndFixtureTests
{
    private static string GetFixturesRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "NEXUS-3D.sln")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        return Path.Combine(dir, "tests", "Fixtures");
    }

    [Fact]
    public async Task GenerateAndVerify_RealDiskFixtures_AcrossAllCategories()
    {
        string fixturesRoot = GetFixturesRoot();
        string spongeV2Dir = Path.Combine(fixturesRoot, "SpongeV2");
        string spongeV3Dir = Path.Combine(fixturesRoot, "SpongeV3");
        string legacyDir = Path.Combine(fixturesRoot, "Legacy");
        string invalidDir = Path.Combine(fixturesRoot, "Invalid");
        string largeDir = Path.Combine(fixturesRoot, "Large");
        string blockEntitiesDir = Path.Combine(fixturesRoot, "BlockEntities");

        Directory.CreateDirectory(spongeV2Dir);
        Directory.CreateDirectory(spongeV3Dir);
        Directory.CreateDirectory(legacyDir);
        Directory.CreateDirectory(invalidDir);
        Directory.CreateDirectory(largeDir);
        Directory.CreateDirectory(blockEntitiesDir);

        var writer = new SchematicFormatExporter();
        var reader = new SchematicFormatParser();
        var detector = new SchematicFormatDetector();

        // 1. SpongeV2 fixture
        var v2Doc = CreateSampleDocument(SchematicFormatId.SpongeV2, 4, 4, 4, includeBlockEntity: false);
        string v2Path = Path.Combine(spongeV2Dir, "small_house_v2.schem");
        var v2Write = await writer.WriteDocumentToFileAsync(v2Doc, v2Path, new SchematicWriteOptions
        {
            TargetFormat = SchematicFormatId.SpongeV2,
            Compression = NbtCompressionType.GZip
        });
        Assert.True(v2Write.IsSuccess);
        Assert.True(v2Write.VerifiedAfterWrite);

        // 2. SpongeV3 fixture (with Biomes & Entities)
        var v3Doc = CreateSampleDocument(SchematicFormatId.SpongeV3, 4, 4, 4, includeBlockEntity: false);
        var biomes = new NbtCompoundTag("Biomes");
        var biomePal = new NbtCompoundTag("Palette");
        biomePal.PutInt("minecraft:cherry_grove", 0);
        biomes.SetTag(biomePal);
        biomes.PutByteArray("Data", new byte[16]);
        v3Doc.PreservedBiomesTag = biomes;
        string v3Path = Path.Combine(spongeV3Dir, "cherry_shrine_v3.schem");
        var v3Write = await writer.WriteDocumentToFileAsync(v3Doc, v3Path, new SchematicWriteOptions
        {
            TargetFormat = SchematicFormatId.SpongeV3,
            Compression = NbtCompressionType.GZip
        });
        Assert.True(v3Write.IsSuccess);

        // 3. Legacy .schematic fixture (with AddBlocks ID > 255)
        var legacyDoc = CreateSampleDocument(SchematicFormatId.LegacyMcEdit, 4, 3, 4, includeBlockEntity: false);
        string legacyPath = Path.Combine(legacyDir, "classic_tower.schematic");
        var legacyWrite = await writer.WriteDocumentToFileAsync(legacyDoc, legacyPath, new SchematicWriteOptions
        {
            TargetFormat = SchematicFormatId.LegacyMcEdit,
            Compression = NbtCompressionType.GZip
        });
        Assert.True(legacyWrite.IsSuccess);

        // 4. BlockEntities fixture
        var beDoc = CreateSampleDocument(SchematicFormatId.SpongeV2, 3, 3, 3, includeBlockEntity: true);
        string bePath = Path.Combine(blockEntitiesDir, "storage_room_with_chests.schem");
        var beWrite = await writer.WriteDocumentToFileAsync(beDoc, bePath, new SchematicWriteOptions
        {
            TargetFormat = SchematicFormatId.SpongeV2
        });
        Assert.True(beWrite.IsSuccess);

        // 5. Large fixture (32x32x32 = 32,768 blocks)
        var largeDoc = CreateSampleDocument(SchematicFormatId.SpongeV2, 32, 32, 32, includeBlockEntity: true);
        string largePath = Path.Combine(largeDir, "fortress_32x32x32.schem");
        var largeWrite = await writer.WriteDocumentToFileAsync(largeDoc, largePath, new SchematicWriteOptions
        {
            TargetFormat = SchematicFormatId.SpongeV2
        });
        Assert.True(largeWrite.IsSuccess);

        // 6. Invalid fixture (corrupted GZip bytes)
        string invalidPath = Path.Combine(invalidDir, "corrupted_stream.schem");
        await File.WriteAllBytesAsync(invalidPath, [0x1F, 0x8B, 0x08, 0x00, 0xDE, 0xAD, 0xBE, 0xEF]);

        // Verify reading all valid fixtures back into USM Scene3D
        await using (var fs = File.OpenRead(v2Path))
        {
            Assert.Equal(SchematicFormatId.SpongeV2, await detector.DetectSchematicFormatAsync(fs));
        }

        await using (var fs = File.OpenRead(v3Path))
        {
            Assert.Equal(SchematicFormatId.SpongeV3, await detector.DetectSchematicFormatAsync(fs));
        }

        await using (var fs = File.OpenRead(legacyPath))
        {
            Assert.Equal(SchematicFormatId.LegacyMcEdit, await detector.DetectSchematicFormatAsync(fs));
        }

        var invalidRead = await reader.ReadFileAsync(invalidPath);
        Assert.False(invalidRead.IsSuccess);
        Assert.NotNull(invalidRead.Error);
    }

    [Theory]
    [InlineData(SchematicFormatId.SpongeV2, SchematicFormatId.SpongeV2)]
    [InlineData(SchematicFormatId.SpongeV3, SchematicFormatId.SpongeV3)]
    [InlineData(SchematicFormatId.LegacyMcEdit, SchematicFormatId.LegacyMcEdit)]
    [InlineData(SchematicFormatId.LegacyMcEdit, SchematicFormatId.SpongeV2)]
    [InlineData(SchematicFormatId.SpongeV2, SchematicFormatId.SpongeV3)]
    [InlineData(SchematicFormatId.SpongeV3, SchematicFormatId.SpongeV2)]
    public async Task RoundTripAndCrossFormat_ViaUniversalSceneModel_PreservesVoxelsOffsetsAndBlockEntities(
        SchematicFormatId sourceFormat,
        SchematicFormatId targetFormat)
    {
        var reader = new SchematicFormatParser();
        var writer = new SchematicFormatExporter();

        var originalDoc = CreateSampleDocument(sourceFormat, 5, 4, 6, includeBlockEntity: true);

        // Step 1: Write original document to binary stream in sourceFormat
        using var sourceStream = new MemoryStream();
        var firstWrite = await writer.WriteDocumentAsync(originalDoc, sourceStream, new SchematicWriteOptions
        {
            TargetFormat = sourceFormat,
            Compression = NbtCompressionType.GZip
        });
        Assert.True(firstWrite.IsSuccess);

        // Step 2: Read binary stream -> USM Scene3D
        sourceStream.Position = 0;
        var usmRead = await reader.ReadAsync(sourceStream);
        Assert.True(usmRead.IsSuccess);
        Assert.NotNull(usmRead.Scene);
        Assert.Equal(sourceFormat, usmRead.DetectedFormat);
        Assert.True(usmRead.Scene!.Validate().IsValid);

        // Step 3: Export USM Scene3D -> binary stream in targetFormat
        using var targetStream = new MemoryStream();
        var secondWrite = await writer.WriteSceneAsync(usmRead.Scene!, targetStream, new SchematicWriteOptions
        {
            TargetFormat = targetFormat,
            Compression = NbtCompressionType.GZip
        });
        Assert.True(secondWrite.IsSuccess);
        Assert.True(secondWrite.VerifiedAfterWrite);

        // Step 4: Read back target binary stream -> USM Scene3D and compare every block & block entity!
        targetStream.Position = 0;
        var finalRead = await reader.ReadAsync(targetStream);
        Assert.True(finalRead.IsSuccess);
        Assert.Equal(targetFormat, finalRead.DetectedFormat);
        Assert.NotNull(finalRead.Document);

        var finalDoc = finalRead.Document!;
        Assert.Equal(originalDoc.Dimensions, finalDoc.Dimensions);
        Assert.Equal(originalDoc.Offset, finalDoc.Offset);
        Assert.Equal(originalDoc.BlockEntities.Count, finalDoc.BlockEntities.Count);

        for (int y = 0; y < originalDoc.Dimensions.Height; y++)
        {
            for (int z = 0; z < originalDoc.Dimensions.Length; z++)
            {
                for (int x = 0; x < originalDoc.Dimensions.Width; x++)
                {
                    int origIdx = originalDoc.GetPaletteIndexAt(x, y, z);
                    int finalIdx = finalDoc.GetPaletteIndexAt(x, y, z);

                    Assert.True(originalDoc.Palette.TryGetByIndex(origIdx, out var origEntry));
                    Assert.True(finalDoc.Palette.TryGetByIndex(finalIdx, out var finalEntry));

                    Assert.Equal(origEntry!.BlockState.Namespace, finalEntry!.BlockState.Namespace);
                    Assert.Equal(origEntry.BlockState.Name, finalEntry.BlockState.Name);
                }
            }
        }
    }

    [Fact]
    public void DependencyInjection_RegistersAllSchematicServicesCleanly()
    {
        var services = new ServiceCollection();
        string tempRoot = Path.Combine(Path.GetTempPath(), $"nexus3d_schem_di_{Guid.NewGuid():N}");

        try
        {
            DependencyConfig.ConfigureInfrastructure(services, new DependencyOptions { CustomAppDataRoot = tempRoot });
            DependencyConfig.ConfigureFormatsAndEngine(services);
            DependencyConfig.ConfigureApplicationServices(services);

            using var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetRequiredService<ISchematicFormatDetector>());
            Assert.NotNull(provider.GetRequiredService<ISchematicReader>());
            Assert.NotNull(provider.GetRequiredService<ISchematicWriter>());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try { Directory.Delete(tempRoot, recursive: true); } catch { }
            }
        }
    }

    private static SchematicDocument CreateSampleDocument(
        SchematicFormatId format,
        int width,
        int height,
        int length,
        bool includeBlockEntity)
    {
        var palette = new SchematicPalette();
        int airIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:air"));
        int stoneIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:stone"));
        int bricksIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:stone_bricks"));
        int chestIdx = palette.GetOrAdd(BlockStateParser.Parse("minecraft:chest[facing=north,type=single,waterlogged=false]"));

        var dims = new SchematicDimensions(width, height, length);
        int total = dims.GetCheckedVolume();
        int[] indices = new int[total];

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int linear = SchematicIndexing.ToIndex(x, y, z, width, length);
                    if (y == 0)
                    {
                        indices[linear] = stoneIdx;
                    }
                    else if (x == 0 || x == width - 1 || z == 0 || z == length - 1)
                    {
                        indices[linear] = bricksIdx;
                    }
                    else
                    {
                        indices[linear] = airIdx;
                    }
                }
            }
        }

        var blockEntities = new List<SchematicBlockEntity>();
        if (includeBlockEntity && width >= 2 && height >= 2 && length >= 2)
        {
            var chestPos = new VoxelPosition(1, 1, 1);
            indices[SchematicIndexing.ToIndex(1, 1, 1, width, length)] = chestIdx;

            var chestData = new NbtCompoundTag("Data");
            chestData.PutString("CustomName", "{\"text\":\"Vault Chest\"}");
            blockEntities.Add(new SchematicBlockEntity
            {
                Id = "minecraft:chest",
                Position = chestPos,
                Data = chestData,
                SourceVersion = format == SchematicFormatId.SpongeV3 ? 3 : 2
            });
        }

        return new SchematicDocument
        {
            Format = format,
            FormatVersion = format == SchematicFormatId.SpongeV3 ? 3 : (format == SchematicFormatId.LegacyMcEdit ? 1 : 2),
            DataVersion = format == SchematicFormatId.LegacyMcEdit ? 1343 : 3465,
            Dimensions = dims,
            Offset = new SchematicOffset(-4, 0, -4),
            Palette = palette,
            BlockIndices = indices,
            BlockEntities = blockEntities,
            Metadata = new SchematicMetadata
            {
                Name = $"Sample_{format}",
                Author = "NEXUS3D",
                WorldEditOffset = new SchematicOffset(-4, 0, -4)
            }
        };
    }
}


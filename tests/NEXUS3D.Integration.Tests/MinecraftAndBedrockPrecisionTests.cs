using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json.Nodes;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.BedrockGeometry.Exporting;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Conversion;
using NEXUS3D.Formats.Schematic.Nbt.Compression;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Sponge;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Integration.Tests;

public sealed class MinecraftAndBedrockPrecisionTests : IDisposable
{
    private readonly string _testTempDir;

    public MinecraftAndBedrockPrecisionTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), $"Nexus3D_PrecisionTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void SchematicIndexing_StrictlyFollows_SpongeYZXFormula()
    {
        int width = 16;
        int height = 32;
        int length = 24;

        // Sponge specification: index = (y * length + z) * width + x
        for (int y = 0; y < 5; y++)
        {
            for (int z = 0; z < 5; z++)
            {
                for (int x = 0; x < 5; x++)
                {
                    int expectedIndex = (y * length + z) * width + x;
                    int computedIndex = SchematicIndexing.ToIndex(x, y, z, width, length);
                    Assert.Equal(expectedIndex, computedIndex);

                    var recovered = SchematicIndexing.FromIndex(computedIndex, width, height, length);
                    Assert.Equal(x, recovered.X);
                    Assert.Equal(y, recovered.Y);
                    Assert.Equal(z, recovered.Z);
                }
            }
        }
    }

    [Fact]
    public void BlockMaterialRegistry_ClassifiesTransparencyCategoriesAccurately()
    {
        var glass = BlockMaterialRegistry.GetBlockInfo("minecraft:glass");
        Assert.Equal(BlockTransparencyKind.Transparent, glass.Transparency);
        Assert.True(glass.Opacity < 1.0f);

        var water = BlockMaterialRegistry.GetBlockInfo("minecraft:water");
        Assert.Equal(BlockTransparencyKind.Translucent, water.Transparency);
        Assert.True(water.Opacity < 1.0f);

        var leaves = BlockMaterialRegistry.GetBlockInfo("minecraft:oak_leaves");
        Assert.Equal(BlockTransparencyKind.Cutout, leaves.Transparency);

        var stone = BlockMaterialRegistry.GetBlockInfo("minecraft:stone");
        Assert.Equal(BlockTransparencyKind.Opaque, stone.Transparency);
        Assert.Equal(1.0f, stone.Opacity);

        var planks = BlockMaterialRegistry.GetBlockInfo("minecraft:oak_planks");
        Assert.Equal(BlockTransparencyKind.Opaque, planks.Transparency);
    }

    [Fact]
    public void BedrockTextureAtlasGenerator_GeneratesCompliantRgbaPng()
    {
        var colorMap = new Dictionary<string, Vector4>(StringComparer.OrdinalIgnoreCase)
        {
            ["stone"] = new(0.5f, 0.5f, 0.5f, 1.0f),
            ["water"] = new(0.2f, 0.4f, 0.9f, 0.6f),
            ["glass"] = new(0.8f, 0.9f, 0.95f, 0.4f)
        };

        var (pngBytes, swatchUvs, width, height) = BedrockTextureAtlasGenerator.GenerateAtlas(colorMap, 64);

        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 64);
        Assert.True(width >= 64);
        Assert.True(height >= 64);

        // Standard PNG Magic Number
        byte[] expectedHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(expectedHeader, pngBytes.Take(8).ToArray());

        Assert.Equal(3, swatchUvs.Count);
        Assert.True(swatchUvs.ContainsKey("stone"));
        Assert.True(swatchUvs.ContainsKey("water"));
        Assert.True(swatchUvs.ContainsKey("glass"));
    }

    [Fact]
    public async Task BedrockResourcePackExporter_CreatesCompleteMcPackZipArchive()
    {
        var scene = new Scene3D { Name = "test_robot" };
        var mat = new MaterialData("robot_body")
        {
            BaseColor = new MaterialColor(0.8, 0.3, 0.2, 1.0),
            Opacity = 1.0
        };
        scene.Materials.Add(mat);

        var grid = new VoxelGrid("body_grid");
        int ironIdx = grid.Palette.GetOrAdd(new BlockState("minecraft", "iron_block"));
        int goldIdx = grid.Palette.GetOrAdd(new BlockState("minecraft", "gold_block"));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), ironIdx, new BlockState("minecraft", "iron_block")));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 0), goldIdx, new BlockState("minecraft", "gold_block")));

        var sceneObj = new SceneObject("RobotObject");
        sceneObj.VoxelGrids.Add(grid);
        scene.Graph.AddNode(scene.Graph.Root, new SceneNode("RobotNode", sceneObj));

        string mcPackPath = Path.Combine(_testTempDir, "test_robot.mcpack");

        var exporter = new BedrockResourcePackExporter();
        string resultPath = await exporter.ExportResourcePackAsync(scene, mcPackPath, createMcPackZip: true);

        Assert.True(File.Exists(resultPath));
        Assert.Equal(mcPackPath, resultPath);

        // Verify ZIP contents
        using var zip = ZipFile.OpenRead(mcPackPath);
        Assert.Contains(zip.Entries, e => e.FullName == "manifest.json");
        Assert.Contains(zip.Entries, e => e.FullName.StartsWith("models/entity/") && e.FullName.EndsWith(".geo.json"));
        Assert.Contains(zip.Entries, e => e.FullName.StartsWith("textures/entity/") && e.FullName.EndsWith(".png"));
        Assert.Contains(zip.Entries, e => e.FullName == "textures/item_texture.json");

        // Verify manifest.json structure
        var manifestEntry = zip.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);
        using var stream = manifestEntry.Open();
        using var reader = new StreamReader(stream);
        string manifestText = await reader.ReadToEndAsync();

        var json = JsonNode.Parse(manifestText);
        Assert.NotNull(json);
        Assert.Equal(2, json["format_version"]?.GetValue<int>());
        Assert.NotNull(json["header"]?["uuid"]);
        Assert.NotNull(json["modules"]?[0]?["uuid"]);
    }

    [Fact]
    public async Task SpongeSchematic_RoundTrip_PreservesBlockStatesWithoutSubstitution()
    {
        var scene = new Scene3D { Name = "SchemPrecisionTest" };
        var grid = new VoxelGrid("MainGrid");

        var stoneState = new BlockState("minecraft", "stone");
        var planksState = new BlockState("minecraft", "oak_planks");
        var glassState = new BlockState("minecraft", "glass");
        var waterState = new BlockState("minecraft", "water");

        int stoneIdx = grid.Palette.GetOrAdd(stoneState);
        int planksIdx = grid.Palette.GetOrAdd(planksState);
        int glassIdx = grid.Palette.GetOrAdd(glassState);
        int waterIdx = grid.Palette.GetOrAdd(waterState);

        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), stoneIdx, stoneState));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 0), planksIdx, planksState));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 1, 0), glassIdx, glassState));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 1), waterIdx, waterState));

        var sceneObj = new SceneObject("GridObj");
        sceneObj.VoxelGrids.Add(grid);
        scene.Graph.AddNode(scene.Graph.Root, new SceneNode("GridNode", sceneObj));

        // 1. Convert USM Scene to Schematic Document
        var toSchem = new SceneToSchematicConverter();
        var (doc, _) = toSchem.Convert(scene, new SchematicMappingOptions
        {
            TargetFormat = SchematicFormatId.SpongeV2,
            DefaultDataVersion = 3953
        });

        Assert.Equal(5, doc.Palette.Count); // air + stone + oak_planks + glass + water

        // 2. Export to GZip NBT
        var writer = new SpongeSchematicWriter();
        using var memory = new MemoryStream();
        await writer.WriteAsync(doc, memory, new SchematicWriteOptions
        {
            TargetFormat = SchematicFormatId.SpongeV2,
            DataVersion = 3953,
            Compression = NbtCompressionType.GZip
        });

        byte[] nbtBytes = memory.ToArray();
        Assert.True(nbtBytes.Length > 0);

        // 3. Read back via NbtReader and SpongeV2Parser
        using var readMemory = new MemoryStream(nbtBytes);
        var nbtReader = new NbtReader();
        var nbtDoc = await nbtReader.ReadAsync(readMemory, new NbtReadOptions
        {
            Compression = NbtCompressionType.AutoDetect
        });

        var parser = new SpongeV2Parser();
        var parsedDoc = await parser.ParseAsync(nbtDoc);

        // 4. Assertions on parsed document
        Assert.Equal(3953, parsedDoc.DataVersion);
        Assert.Equal(SchematicFormatId.SpongeV2, parsedDoc.Format);

        // Check palette contains our block states
        var paletteNames = parsedDoc.Palette.EntriesByIndex.Values.Select(e => e.BlockState.GetFullIdentifier()).ToList();
        Assert.Contains("minecraft:stone", paletteNames);
        Assert.Contains("minecraft:oak_planks", paletteNames);
        Assert.Contains("minecraft:glass", paletteNames);
        Assert.Contains("minecraft:water", paletteNames);

        // 5. Convert back to USM Scene
        var toScene = new SchematicToSceneConverter();
        var (recoveredScene, _) = toScene.Convert(parsedDoc);

        var recoveredGrid = Assert.Single(recoveredScene.Graph.Nodes.SelectMany(n => n.Object?.VoxelGrids ?? []));

        // Test non-air count
        var voxels = recoveredGrid.Voxels.Where(v => !v.IsAir).ToList();
        Assert.Equal(4, voxels.Count);

        var recoveredIds = voxels.Select(v => v.State?.GetFullIdentifier()).ToList();
        Assert.Contains("minecraft:stone", recoveredIds);
        Assert.Contains("minecraft:oak_planks", recoveredIds);
        Assert.Contains("minecraft:glass", recoveredIds);
        Assert.Contains("minecraft:water", recoveredIds);
    }
}

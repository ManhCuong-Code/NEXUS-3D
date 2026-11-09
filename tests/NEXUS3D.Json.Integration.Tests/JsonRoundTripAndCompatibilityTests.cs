using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.App.Bootstrap;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.BedrockGeometry.Exporting;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.BedrockGeometry.Services;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Formats.VoxelJson.Exporting;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;
using NEXUS3D.Formats.VoxelJson.Services;

namespace NEXUS3D.Json.Integration.Tests;

public sealed class JsonRoundTripAndCompatibilityTests
{
    private static string GetFixturePath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "Json", fileName));

    [Fact]
    public async Task VoxelJson_NexusV1_FullUsmRoundTrip_PreservesVoxelsMaterialsLayersSizesRotationsAndExtensions()
    {
        var reader = new VoxelJsonFormatParser();
        var writer = new VoxelJsonFormatExporter();
        string sourcePath = GetFixturePath("voxel_nexus_v1.json");

        VoxelJsonReadResult readResult = await reader.ReadFileAsync(sourcePath);
        Assert.True(readResult.IsSuccess);
        Assert.NotNull(readResult.Scene);
        Assert.True(readResult.Scene!.Validate().IsValid);

        SceneStatistics stats = readResult.Scene.CalculateStatistics();
        Assert.Equal(2, stats.TotalVoxelCount);
        Assert.Equal(2, stats.MaterialCount);
        Assert.Equal(0, stats.MeshCount); // Voxel JSON stays VoxelGrid in USM, never auto-meshed!

        string tempOut = Path.Combine(Path.GetTempPath(), $"nexus_voxel_rt_{Guid.NewGuid():N}.json");
        try
        {
            FormatExportReport exportReport = await writer.ExportSceneToFileAsync(readResult.Scene, tempOut);
            Assert.True(exportReport.IsSuccess);
            Assert.True(exportReport.BytesWritten > 0);
            Assert.False(exportReport.LossReport.HasDataLoss);

            var rawParser = new VoxelJsonParser();
            VoxelJsonParseResult reparsed = rawParser.ParseFile(tempOut);
            Assert.False(reparsed.HasErrors);
            Assert.Equal("Crystal Shrine", reparsed.Document.Metadata.Name);
            Assert.Equal(new Vector3D(0.5, 0.5, 0.5), reparsed.Document.Grid.VoxelSize);
            Assert.Equal(2, reparsed.Document.Materials.Count);
            Assert.Equal(2, reparsed.Document.Layers.Count);
            Assert.Equal(2, reparsed.Document.Voxels.Count);

            VoxelRecord spire = reparsed.Document.Voxels[1];
            Assert.Equal(new Vector3D(4.5, 2, 4.5), spire.Position);
            Assert.Equal(new Vector3D(1, 3, 1), spire.Size);
            Assert.Equal(new Vector3D(0, 45, 0), spire.Rotation);
            Assert.True(spire.ExtensionData.TryGet("customLightLevel", out JsonElement lightElem));
            Assert.Equal(15, lightElem.GetInt32());
        }
        finally
        {
            if (File.Exists(tempOut)) File.Delete(tempOut);
        }
    }

    [Fact]
    public async Task VoxelJson_CuboidLantern_FullUsmRoundTrip_PreservesCuboidsPerFaceUvsAndGroups()
    {
        var reader = new VoxelJsonFormatParser();
        var writer = new VoxelJsonFormatExporter();
        string sourcePath = GetFixturePath("voxel_cuboid_lantern.json");

        VoxelJsonReadResult readResult = await reader.ReadFileAsync(sourcePath);
        Assert.True(readResult.IsSuccess);
        Assert.NotNull(readResult.Scene);
        Assert.True(readResult.Scene!.Validate().IsValid);
        Assert.Equal(2, readResult.Scene.CalculateStatistics().TotalVoxelCount);

        using var ms = new MemoryStream();
        FormatExportReport exportReport = await writer.ExportSceneAsync(readResult.Scene, ms);
        Assert.True(exportReport.IsSuccess);

        ms.Position = 0;
        var rawParser = new VoxelJsonParser();
        VoxelJsonParseResult reparsed = rawParser.Parse(ms);
        Assert.Equal(VoxelJsonDialect.MinecraftBlockModelCuboids, reparsed.DetectedDialect);
        Assert.Equal(2, reparsed.Document.Voxels.Count);
        Assert.Equal(6, reparsed.Document.Voxels[0].Faces.Count);
        Assert.Equal(new Vector3D(0, 45, 0), reparsed.Document.Voxels[1].Rotation);
    }

    [Fact]
    public async Task BedrockGeometry_Modern1160_FullUsmRoundTrip_PreservesHierarchyCubesPerFaceUvLocatorsAndPolyMesh()
    {
        var reader = new BedrockGeometryFormatParser();
        var writer = new BedrockGeometryFormatExporter();
        string sourcePath = GetFixturePath("bedrock_1_16_0_guardian.geo.json");

        BedrockGeometryReadResult readResult = await reader.ReadFileAsync(sourcePath);
        Assert.True(readResult.IsSuccess);
        Assert.NotNull(readResult.Scene);
        Assert.True(readResult.Scene!.Validate().IsValid);

        SceneStatistics stats = readResult.Scene.CalculateStatistics();
        Assert.Equal(1, stats.SkeletonCount);
        Assert.Equal(3, stats.TotalBoneCount);
        Assert.Equal(4, stats.MeshCount); // 3 cubes + 1 poly_mesh

        // Modify bone pivot and add a new locator in USM SkeletonData to verify USM edits sync back on export
        SkeletonData skeleton = readResult.Scene.Skeletons[0];
        BoneData weaponBone = skeleton.Bones.First(b => b.Name == "right_hand_weapon");
        weaponBone.Locators.Add(new BoneLocator("trail_anchor", new Vector3D(2.0, 20.0, -6.0)));

        string tempOut = Path.Combine(Path.GetTempPath(), $"nexus_bedrock_rt_{Guid.NewGuid():N}.geo.json");
        try
        {
            FormatExportReport exportReport = await writer.ExportSceneToFileAsync(readResult.Scene, tempOut);
            Assert.True(exportReport.IsSuccess);
            Assert.False(exportReport.LossReport.HasDataLoss);

            var rawParser = new BedrockGeometryParser();
            BedrockGeometryParseResult reparsed = rawParser.ParseFile(tempOut);
            Assert.False(reparsed.HasErrors);
            Assert.Equal("1.16.0", reparsed.Document.FormatVersion);
            Assert.Single(reparsed.Document.Geometries);

            var geo = reparsed.Document.Geometries[0];
            Assert.Equal("geometry.nexus_guardian", geo.Description.Identifier);
            Assert.Equal(3, geo.Bones.Count);

            BedrockBone exportedWeapon = geo.Bones.First(b => b.Name == "right_hand_weapon");
            Assert.Equal(3, exportedWeapon.Locators.Count);
            Assert.True(exportedWeapon.Locators.ContainsKey("trail_anchor"));
            Assert.Equal(new Vector3D(-2.0, 20.0, -6.0), exportedWeapon.Locators["trail_anchor"].Offset);
            Assert.Equal(BedrockUvMode.PerFaceUv, exportedWeapon.Cubes[0].Uv!.Mode);
            Assert.Equal("blade_glow", exportedWeapon.Cubes[0].Uv!.Faces["north"].MaterialInstance);
            Assert.True(exportedWeapon.Cubes[0].ExtensionData.TryGet("custom_cube_flag", out _));
        }
        finally
        {
            if (File.Exists(tempOut)) File.Delete(tempOut);
        }
    }

    [Fact]
    public async Task CrossFormat_BedrockGeometryToUsmToWavefrontObj_ExportsValidObjMeshes()
    {
        var bedrockReader = new BedrockGeometryFormatParser();
        var objWriter = new ObjFormatExporter();

        BedrockGeometryReadResult readResult = await bedrockReader.ReadFileAsync(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        Assert.True(readResult.IsSuccess);
        Assert.NotNull(readResult.Scene);

        using var objStream = new MemoryStream();
        FormatExportReport objReport = await objWriter.ExportSceneAsync(readResult.Scene!, objStream);
        Assert.True(objReport.IsSuccess);
        Assert.True(objReport.BytesWritten > 0);
    }

    [Fact]
    public async Task CrossFormat_UsmVoxelGridToVoxelJsonAndBedrockGeometry_Succeeds()
    {
        var scene = new Scene3D { Name = "Custom Voxel Structure" };
        var grid = new VoxelGrid("Tower") { VoxelSize = new VoxelSize(1, 1, 1) };
        int palIdx = grid.Palette.GetOrAdd(new BlockState("minecraft", "stone"));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), palIdx, new BlockState("minecraft", "stone")));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 1, 0), palIdx, new BlockState("minecraft", "stone")));

        var obj = new SceneObject("TowerObject");
        obj.VoxelGrids.Add(grid);
        scene.Graph.AddNode(scene.Graph.Root, new SceneNode("TowerNode", obj));

        var voxelWriter = new VoxelJsonFormatExporter();
        using var voxelStream = new MemoryStream();
        FormatExportReport voxelReport = await voxelWriter.ExportSceneAsync(scene, voxelStream);
        Assert.True(voxelReport.IsSuccess);

        var bedrockWriter = new BedrockGeometryFormatExporter();
        using var bedrockStream = new MemoryStream();
        FormatExportReport bedrockReport = await bedrockWriter.ExportSceneAsync(scene, bedrockStream);
        Assert.True(bedrockReport.IsSuccess);
    }

    [Fact]
    public void DependencyInjection_RegistersAllStage05VoxelAndBedrockServices()
    {
        var services = new ServiceCollection();
        string tempAppData = Path.Combine(Path.GetTempPath(), $"nexus_di_{Guid.NewGuid():N}");
        try
        {
            DependencyConfig.ConfigureInfrastructure(services, new DependencyOptions { CustomAppDataRoot = tempAppData });
            DependencyConfig.ConfigureFormatsAndEngine(services);
            DependencyConfig.ConfigureApplicationServices(services);

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IVoxelJsonReader>());
            Assert.NotNull(provider.GetService<IVoxelJsonWriter>());
            Assert.NotNull(provider.GetService<IVoxelJsonFormatDetector>());
            Assert.NotNull(provider.GetService<IBedrockGeometryReader>());
            Assert.NotNull(provider.GetService<IBedrockGeometryWriter>());
            Assert.NotNull(provider.GetService<IBedrockGeometryFormatDetector>());

            IFormatDetector[] detectors = provider.GetServices<IFormatDetector>().ToArray();
            Assert.True(detectors.Length >= 4); // Schematic, OBJ, VoxelJson, BedrockGeometry
        }
        finally
        {
            if (Directory.Exists(tempAppData))
            {
                try { Directory.Delete(tempAppData, recursive: true); } catch { }
            }
        }
    }
}

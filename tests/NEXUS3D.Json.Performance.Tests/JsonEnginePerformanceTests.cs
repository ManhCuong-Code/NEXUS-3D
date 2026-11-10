using System.Diagnostics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Exporting;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.VoxelJson.Conversion;
using NEXUS3D.Formats.VoxelJson.Exporting;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;

namespace NEXUS3D.Json.Performance.Tests;

public sealed class JsonEnginePerformanceTests
{
    [Fact]
    public void VoxelJson_25000Voxels_ParseConvertAndExport_CompletesWithinPerformanceBudget()
    {
        var doc = new VoxelDocument
        {
            Dialect = VoxelJsonDialect.NexusVoxelGridV1
        };
        doc.Metadata.Name = "LargeVoxelBenchmark";
        doc.Grid.Dimensions = new Vector3D(50, 10, 50);
        doc.Materials.Add(new VoxelMaterialReference
        {
            Id = "stone",
            Name = "Stone",
            Color = new VoxelColor(0.5, 0.5, 0.5, 1.0)
        });

        for (int y = 0; y < 10; y++)
        {
            for (int z = 0; z < 50; z++)
            {
                for (int x = 0; x < 50; x++)
                {
                    doc.Voxels.Add(new VoxelRecord
                    {
                        Position = new Vector3D(x, y, z),
                        MaterialId = "stone"
                    });
                }
            }
        }

        var exporter = new VoxelJsonExporter();
        var parser = new VoxelJsonParser();
        var toScene = new VoxelJsonToSceneConverter();
        var fromScene = new SceneToVoxelJsonConverter();

        var sw = Stopwatch.StartNew();
        byte[] utf8Bytes = exporter.ExportToUtf8Bytes(doc, new VoxelJsonWriteOptions { PrettyPrint = false, VerifyAfterWrite = false });
        VoxelJsonParseResult parsed = parser.Parse(utf8Bytes);
        var (scene, _) = toScene.Convert(parsed.Document);
        var (reconstructed, _) = fromScene.Convert(scene);
        sw.Stop();

        Assert.Equal(25_000, parsed.Document.Voxels.Count);
        Assert.Equal(25_000, reconstructed.Voxels.Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"Expected < 10s for 25,000 voxels, actual: {sw.Elapsed}");
    }

    [Fact]
    public void BedrockGeometry_200Bones1000Cubes_ParseConvertAndExport_CompletesWithinPerformanceBudget()
    {
        var geo = new BedrockGeometry
        {
            Description = new BedrockGeometryDescription
            {
                Identifier = "geometry.large_benchmark",
                TextureWidth = 128,
                TextureHeight = 128
            }
        };

        for (int b = 0; b < 200; b++)
        {
            var bone = new BedrockBone
            {
                Name = $"bone_{b}",
                Parent = b == 0 ? null : $"bone_{(b - 1) / 2}",
                Pivot = new Vector3D(0, b * 2, 0),
                Rotation = new Vector3D(0, b % 45, 0)
            };

            for (int c = 0; c < 5; c++)
            {
                bone.Cubes.Add(new BedrockCube
                {
                    Origin = new Vector3D(-2 + c, b * 2, -2),
                    Size = new Vector3D(2, 2, 2),
                    Uv = BedrockCubeUv.CreateBoxUv(0, 0)
                });
            }

            geo.Bones.Add(bone);
        }

        var doc = new BedrockGeometryDocument
        {
            FormatVersion = "1.16.0",
            SchemaFamily = BedrockGeometrySchemaFamily.Modern1160Plus,
            Geometries = [geo]
        };

        var exporter = new BedrockGeometryExporter();
        var parser = new BedrockGeometryParser();
        var toScene = new BedrockGeometryToSceneConverter();
        var fromScene = new SceneToBedrockGeometryConverter();

        var sw = Stopwatch.StartNew();
        byte[] utf8Bytes = exporter.ExportToUtf8Bytes(doc, new BedrockGeometryWriteOptions { PrettyPrint = false, VerifyAfterWrite = false });
        BedrockGeometryParseResult parsed = parser.Parse(utf8Bytes);
        var (scene, report) = toScene.Convert(parsed.Document);
        var (reconstructed, _) = fromScene.Convert(scene);
        sw.Stop();

        Assert.Equal(200, report.BoneCount);
        Assert.Equal(1000, report.CubeCount);
        Assert.Equal(1000, report.GeneratedMeshCount);
        Assert.Equal(200, reconstructed.Geometries[0].Bones.Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"Expected < 10s for 200 bones / 1000 cubes, actual: {sw.Elapsed}");
    }
}

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.App.Bootstrap;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Obj.Conversion;
using NEXUS3D.Formats.Obj.Exporting;
using NEXUS3D.Formats.Obj.Services;

namespace NEXUS3D.Obj.Integration.Tests;

public sealed class ObjRoundTripIntegrationTests
{
    private static string GetFixturePath(string relativePath)
    {
        string baseDir = AppContext.BaseDirectory;
        string? current = baseDir;
        while (current is not null)
        {
            string candidate = Path.Combine(current, "tests", "Fixtures", "Obj", relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new FileNotFoundException($"Could not locate OBJ test fixture: {relativePath}");
    }

    [Fact]
    public async Task TexturedCubeFixture_FullRoundTripWithMtlAndCopiedTextures_PreservesAllGeometryMaterialsAndSeams()
    {
        string fixtureObjPath = GetFixturePath("TexturedCube.obj");
        var parser = new ObjFormatParser();
        var exporter = new ObjFormatExporter();

        // 1. Parse TexturedCube.obj + TexturedCube.mtl + textures/checker.png into USM Scene3D
        ObjReadResult read1 = await parser.ReadFileAsync(fixtureObjPath);
        Assert.True(read1.IsSuccess, read1.Error?.Message);
        Assert.NotNull(read1.Scene);
        Assert.NotNull(read1.ObjDocument);
        Assert.NotNull(read1.MtlDocument);

        // Verify source OBJ counts
        Assert.Equal(8, read1.ObjDocument!.Positions.Count);
        Assert.Equal(4, read1.ObjDocument.TextureCoordinates.Count);
        Assert.Equal(6, read1.ObjDocument.Normals.Count);
        Assert.Equal(6, read1.ObjDocument.Faces.Count);

        // Verify USM MeshData did NOT merge vertices with different UVs/normals at the 8 cube corners (6 quads * 4 corners = 24 distinct (v,vt,vn) vertices)
        var sceneStats = read1.Scene!.CalculateStatistics();
        Assert.Equal(1, sceneStats.MeshCount);
        Assert.Equal(24, sceneStats.TotalVertexCount);
        Assert.Equal(6, sceneStats.TotalFaceCount);
        Assert.Equal(2, sceneStats.MaterialCount);
        Assert.Equal(1, sceneStats.TextureCount);
        Assert.True(read1.Scene.Validate().IsValid);

        // 2. Export Scene3D to a temporary directory with companion MTL + texture copying
        string tempExportDir = Path.Combine(Path.GetTempPath(), $"nexus_obj_rt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempExportDir);

        try
        {
            string exportedObjPath = Path.Combine(tempExportDir, "ExportedCube.obj");
            ObjExportResult exportResult = await exporter.ExportSceneToFileAsync(
                read1.Scene,
                exportedObjPath,
                new ObjExportOptions
                {
                    FloatPrecision = 6,
                    ExportCompanionMtl = true,
                    CopyTextures = true,
                    PreserveTextureDirectoryStructure = true,
                    VerifyAfterExport = true
                });

            Assert.True(exportResult.Succeeded);
            Assert.True(File.Exists(exportResult.ObjFilePath));
            Assert.NotNull(exportResult.MtlFilePath);
            Assert.True(File.Exists(exportResult.MtlFilePath));
            Assert.NotEmpty(exportResult.CopiedTexturePaths);
            Assert.True(File.Exists(Path.Combine(tempExportDir, "textures", "checker.png")));

            // 3. Re-parse exported OBJ + MTL + copied texture
            ObjReadResult read2 = await parser.ReadFileAsync(exportedObjPath);
            Assert.True(read2.IsSuccess, read2.Error?.Message);
            Assert.NotNull(read2.ObjDocument);
            Assert.NotNull(read2.MtlDocument);
            Assert.NotNull(read2.Scene);

            Assert.Equal(8, read2.ObjDocument!.Positions.Count);
            Assert.Equal(4, read2.ObjDocument.TextureCoordinates.Count);
            Assert.Equal(6, read2.ObjDocument.Normals.Count);
            Assert.Equal(6, read2.ObjDocument.Faces.Count);

            // Verify groups, smoothing groups, and material assignments survived round-trip
            Assert.Equal(["FrontBackGroup"], read2.ObjDocument.Faces[0].GroupNames);
            Assert.Equal(1, read2.ObjDocument.Faces[0].SmoothingGroup);
            Assert.Equal("BrickMaterial", read2.ObjDocument.Faces[0].MaterialName);

            Assert.Equal(["SidesGroup"], read2.ObjDocument.Faces[2].GroupNames);
            Assert.Equal(0, read2.ObjDocument.Faces[2].SmoothingGroup);
            Assert.Equal("TrimMaterial", read2.ObjDocument.Faces[2].MaterialName);

            // Verify MTL properties survived round-trip
            Assert.Equal(2, read2.MtlDocument!.Materials.Count);
            var brick = read2.MtlDocument.FindByName("BrickMaterial");
            Assert.NotNull(brick);
            Assert.Equal(64.0, brick!.SpecularExponent);
            Assert.Equal(1.45, brick.OpticalDensity);
            Assert.NotNull(brick.MapKd);
            Assert.Equal("textures/checker.png", brick.MapKd!.TexturePath);
            Assert.NotNull(brick.MapBump);
            Assert.Equal(0.5, brick.MapBump!.BumpMultiplier);
        }
        finally
        {
            if (Directory.Exists(tempExportDir))
            {
                Directory.Delete(tempExportDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PureUsmMeshRoundTrip_WithoutPreservedRawPools_NeverMergesUvSeamsOrHardNormalEdges()
    {
        // Two triangles sharing vertices 1 and 2 in 3D space, but with a UV seam AND a 90-degree hard normal edge!
        const string seamObj = """
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 0 1
            vt 0.0 0.0
            vt 0.5 0.0
            vt 0.5 1.0
            vt 0.5 0.5
            vt 1.0 0.5
            vt 1.0 1.0
            vn 0 0 1
            vn 1 0 0
            f 1/1/1 2/2/1 3/3/1
            f 2/4/2 4/5/2 3/6/2
            """;

        var parser = new ObjFormatParser();
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(seamObj));

        // Parse with PreserveRawData = false so SceneToObjConverter must reconstruct pools purely from USM MeshData
        ObjReadResult read1 = await parser.ReadAsync(
            inputStream,
            new ObjMappingOptions { PreserveRawData = false });

        Assert.True(read1.IsSuccess);
        Assert.NotNull(read1.Scene);

        // Export USM Scene3D to OBJ stream and re-parse
        var exporter = new ObjFormatExporter();
        using var exportedStream = new MemoryStream();
        FormatExportReport exportReport = await exporter.ExportSceneAsync(
            read1.Scene!,
            exportedStream,
            new ExportOptions { PreserveUnknownData = false, VerifyAfterWrite = true });

        Assert.True(exportReport.IsSuccess, exportReport.Error?.Message);

        exportedStream.Position = 0;
        ObjReadResult read2 = await parser.ReadAsync(exportedStream);
        Assert.True(read2.IsSuccess);

        var doc2 = read2.ObjDocument!;
        Assert.Equal(4, doc2.Positions.Count);          // 4 distinct 3D positions
        Assert.Equal(6, doc2.TextureCoordinates.Count); // 6 distinct UVs across the seam
        Assert.Equal(2, doc2.Normals.Count);            // 2 distinct normals across the hard edge
        Assert.Equal(2, doc2.Faces.Count);
    }

    [Fact]
    public async Task DependencyInjection_ResolvesObjServicesDetectorAndFormatRegistry()
    {
        var services = new ServiceCollection();
        DependencyConfig.ConfigureFormatsAndEngine(services);
        await using ServiceProvider sp = services.BuildServiceProvider();

        var registry = sp.GetRequiredService<IFormatRegistry>();
        IFormatDescriptor? objDescriptor = registry.GetByFormatType(ModelFormatType.Obj);
        Assert.NotNull(objDescriptor);
        Assert.Equal(".obj", objDescriptor!.PrimaryExtension);

        var detector = sp.GetRequiredService<IObjFormatDetector>();
        var reader = sp.GetRequiredService<IObjReader>();
        var writer = sp.GetRequiredService<IObjWriter>();

        string fixtureObjPath = GetFixturePath("TexturedCube.obj");
        await using var fs = File.OpenRead(fixtureObjPath);
        FormatDetectionResult detection = await detector.DetectAsync(fs, ".obj");
        Assert.True(detection.IsRecognized);
        Assert.Equal(ModelFormatType.Obj, detection.FormatType);
        Assert.Equal("wavefront:obj", detection.FormatId);

        var summaryResult = await reader.InspectAsync(fixtureObjPath);
        Assert.True(summaryResult.IsSuccess);
        Assert.Equal(6, summaryResult.Value!.ElementCount);

        var targetCheck = await writer.ValidateTargetAsync("output_model.obj");
        Assert.True(targetCheck.IsSuccess);
    }
}


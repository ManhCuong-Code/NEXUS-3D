using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Engine.DependencyInjection;
using NEXUS3D.Conversion.Engine.Services;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Obj;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Infrastructure.Formats;
using NEXUS3D.Sample.Plugin;
using Xunit;

namespace NEXUS3D.Plugin.Integration.Tests;

public sealed class PluginFormatConversionIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;

    public PluginFormatConversionIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "Nexus3D_PluginConvTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public async Task RawVox_ParseAndExport_RoundTripPreservesVoxels()
    {
        // 1. Create a RawVox file with RAWVOX1 magic header
        string rawvoxContent = """
        RAWVOX1
        NAME TestCube
        DIMENSIONS 2 2 2
        PALETTE 2
        0 minecraft:stone
        1 minecraft:gold_block
        VOXELS 4
        0 0 0 0
        1 0 0 1
        0 1 0 1
        1 1 1 0
        VERTICES 8
        0.0 0.0 0.0
        1.0 0.0 0.0
        1.0 1.0 0.0
        0.0 1.0 0.0
        0.0 0.0 1.0
        1.0 0.0 1.0
        1.0 1.0 1.0
        0.0 1.0 1.0
        FACES 2
        0 1 2
        2 3 0
        """;

        string rawvoxPath = Path.Combine(_tempDirectory, "test_cube.rawvox");
        await File.WriteAllTextAsync(rawvoxPath, rawvoxContent);

        // 2. Parse via RawVoxParser using stream
        var parser = new RawVoxParser();
        using var readStream = File.OpenRead(rawvoxPath);
        var parseResult = await parser.ParseToSceneAsync(readStream);

        Assert.True(parseResult.IsSuccess);
        Assert.NotNull(parseResult.Value);

        var scene = parseResult.Value;
        Assert.NotNull(scene.Graph.Root);
        Assert.NotEmpty(scene.Graph.Root.Children);

        var sceneObj = scene.Graph.Root.Children.First().Object;
        Assert.NotNull(sceneObj);
        Assert.NotEmpty(sceneObj.VoxelGrids);
        Assert.Equal(4, sceneObj.VoxelGrids[0].Voxels.Count);
        Assert.Equal(2, sceneObj.VoxelGrids[0].Palette.Count);

        // 3. Export back to another RawVox file
        string exportedPath = Path.Combine(_tempDirectory, "exported_cube.rawvox");
        var exporter = new RawVoxExporter();
        using (var writeStream = File.Create(exportedPath))
        {
            var exportResult = await exporter.ExportSceneAsync(scene, writeStream);
            Assert.True(exportResult.IsSuccess);
        }
        Assert.True(File.Exists(exportedPath));

        // 4. Re-parse exported file and verify voxels preserved
        using var reReadStream = File.OpenRead(exportedPath);
        var reparseResult = await parser.ParseToSceneAsync(reReadStream);
        Assert.True(reparseResult.IsSuccess);
        Assert.NotNull(reparseResult.Value);

        var reSceneObj = reparseResult.Value.Graph.Root.Children.First().Object;
        Assert.NotNull(reSceneObj);
        Assert.NotEmpty(reSceneObj.VoxelGrids);
        Assert.Equal(4, reSceneObj.VoxelGrids[0].Voxels.Count);
    }

    [Fact]
    public async Task UniversalPipeline_RawVoxToObjConversion_Succeeds()
    {
        // 1. Setup DI container with OBJ and RawVox
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());

        var formatRegistry = new FormatRegistry();
        formatRegistry.RegisterDescriptor(new ObjFormatDescriptor());
        formatRegistry.RegisterDescriptor(new RawVoxFormatDescriptor());
        services.AddSingleton<IFormatRegistry>(formatRegistry);

        services.AddObjFormatServices();
        services.AddSingleton<IFormatParser, RawVoxParser>();
        services.AddSingleton<IFormatExporter, RawVoxExporter>();

        services.AddNexusConversionEngine();

        var sp = services.BuildServiceProvider();
        var pipeline = sp.GetRequiredService<UniversalConversionPipeline>();

        // 2. Write rawvox file containing mesh geometry
        string inputPath = Path.Combine(_tempDirectory, "input.rawvox");
        string rawvoxData = """
        RAWVOX1
        NAME SmallMesh
        DIMENSIONS 1 1 1
        PALETTE 1
        0 minecraft:dirt
        VOXELS 1
        0 0 0 0
        VERTICES 3
        0.0 0.0 0.0
        1.0 0.0 0.0
        0.0 1.0 0.0
        FACES 1
        0 1 2
        """;
        await File.WriteAllTextAsync(inputPath, rawvoxData);

        string outputPath = Path.Combine(_tempDirectory, "output.obj");

        // 3. Execute pipeline
        var request = new ConversionRequest(
            SourceFilePath: inputPath,
            TargetFilePath: outputPath,
            SourceFormat: NEXUS3D.Domain.Enums.ModelFormatType.Custom,
            TargetFormat: NEXUS3D.Domain.Enums.ModelFormatType.Obj);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(outputPath));

        string objText = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("v ", objText);
        Assert.Contains("f ", objText);
    }
}

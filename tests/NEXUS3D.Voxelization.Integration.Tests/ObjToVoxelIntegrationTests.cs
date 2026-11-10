using System.Text;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Domain.Models;
using NEXUS3D.Voxelization.Engine.Conversion;
using NEXUS3D.Voxelization.Engine.Engine;
using Xunit;

namespace NEXUS3D.Voxelization.Integration.Tests;

public class ObjToVoxelIntegrationTests
{
    private readonly ObjFormatParser _objParser = new();
    private readonly VoxelizationEngine _voxelEngine;
    private readonly SceneRenderAdapter _renderAdapter = new(
        new MaterialRenderAdapter(new TextureLoader()),
        new BoundingBoxCalculator());

    public ObjToVoxelIntegrationTests()
    {
        var validator = new VoxelizationValidator();
        var cuboidConverter = new VoxelToCuboidConverter();
        _voxelEngine = new VoxelizationEngine(validator, cuboidConverter);
    }

    [Fact]
    public async Task ObjToVoxelToRenderScene_EndToEndPipeline_Succeeds()
    {
        // 1. Create a Wavefront OBJ string (a pyramid with 4 triangular sides and a square base)
        string objContent = """
            # Test Pyramid
            v  0.0  1.0  0.0
            v -1.0 -1.0  1.0
            v  1.0 -1.0  1.0
            v  1.0 -1.0 -1.0
            v -1.0 -1.0 -1.0
            f 1 2 3
            f 1 3 4
            f 1 4 5
            f 1 5 2
            f 2 5 4
            f 2 4 3
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(objContent));
        var parseResult = await _objParser.ParseToSceneAsync(
            stream,
            new ParseOptions { StrictValidation = false },
            CancellationToken.None);

        Assert.True(parseResult.IsSuccess);
        var sourceScene = parseResult.Value!;
        Assert.NotNull(sourceScene);

        // 2. Voxelize the OBJ scene
        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SolidVolume,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 16,
            MergeAdjacentVoxels = true,
            DefaultBlockNamespace = "minecraft:sandstone"
        };

        var voxelResult = await _voxelEngine.VoxelizeAsync(sourceScene, settings);

        Assert.True(voxelResult.IsSuccess);
        Assert.NotNull(voxelResult.VoxelGrid);
        Assert.True(voxelResult.VoxelGrid.GetVoxelCount() > 0);
        Assert.NotNull(voxelResult.Cuboids);
        Assert.True(voxelResult.Cuboids.Count > 0);
        Assert.True(voxelResult.Statistics.TotalTrianglesProcessed == 6);

        // 3. Build USM scene from voxel grid and adapt to RenderScene
        var voxelScene = new Scene3D { Name = "PyramidVoxelScene" };
        var modelNode = new SceneNode("PyramidVoxelNode")
        {
            Object = new SceneObject("PyramidVoxelObject")
            {
                VoxelGrids = { voxelResult.VoxelGrid }
            }
        };
        voxelScene.Graph.AddNode(voxelScene.Graph.Root, modelNode);

        var renderScene = _renderAdapter.Adapt(voxelScene, RenderSettings.Default);

        Assert.NotNull(renderScene);
        Assert.Equal(voxelResult.VoxelGrid.GetVoxelCount(), renderScene.TotalVoxels);
        Assert.NotEmpty(renderScene.RootNodes);
    }
}

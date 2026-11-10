using NEXUS3D.Domain.Scene;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Domain.Models;
using NEXUS3D.Voxelization.Engine.Conversion;
using NEXUS3D.Voxelization.Engine.Engine;
using Xunit;

namespace NEXUS3D.Voxelization.Tests;

public class VoxelizationEngineTests
{
    private readonly VoxelizationEngine _engine;

    public VoxelizationEngineTests()
    {
        var validator = new VoxelizationValidator();
        var cuboidConverter = new VoxelToCuboidConverter();
        _engine = new VoxelizationEngine(validator, cuboidConverter);
    }

    [Fact]
    public async Task VoxelizeAsync_CubeScene_ExecutesPipelineAndProducesGridAndCuboids()
    {
        var cubeMesh = TestMeshFactory.CreateCubeMesh();
        var scene = new Scene3D { Name = "CubeScene" };
        var node = new SceneNode("CubeNode")
        {
            Object = new SceneObject("CubeObject")
            {
                Meshes = { cubeMesh }
            }
        };
        scene.Graph = new SceneGraph(node);

        var progressItems = new List<VoxelizationProgress>();
        var progress = new Progress<VoxelizationProgress>(p => progressItems.Add(p));

        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SurfaceOnly,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 8,
            MergeAdjacentVoxels = true
        };

        var result = await _engine.VoxelizeAsync(scene, settings, progress);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.VoxelGrid);
        Assert.True(result.VoxelGrid.GetVoxelCount() > 0);
        Assert.NotNull(result.Cuboids);
        Assert.True(result.Cuboids.Count > 0);
        Assert.True(result.Statistics.TotalTrianglesProcessed == 12);
        Assert.True(result.Statistics.ProcessingTimeMs >= 0);
    }

    [Fact]
    public async Task VoxelizeAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var cubeMesh = TestMeshFactory.CreateCubeMesh();
        var scene = new Scene3D { Name = "CubeScene" };
        var node = new SceneNode("CubeNode")
        {
            Object = new SceneObject("CubeObject")
            {
                Meshes = { cubeMesh }
            }
        };
        scene.Graph = new SceneGraph(node);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var settings = new VoxelizationSettings();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _engine.VoxelizeAsync(scene, settings, null, cts.Token);
        });
    }

    [Fact]
    public async Task VoxelizeMeshAsync_DirectMeshCall_Succeeds()
    {
        var cubeMesh = TestMeshFactory.CreateCubeMesh();
        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SolidVolume,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 8
        };

        var result = await _engine.VoxelizeMeshAsync(cubeMesh, null, settings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.VoxelGrid);
        Assert.True(result.VoxelGrid.GetVoxelCount() > 0);
    }
}

using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Domain.Models;
using NEXUS3D.Voxelization.Engine.Algorithms;
using Xunit;

namespace NEXUS3D.Voxelization.Tests;

public class SurfaceVoxelizationTests
{
    private readonly SurfaceVoxelization _surfaceAlgorithm = new();

    [Fact]
    public async Task ExecuteAsync_CubeMesh_GeneratesHollowVoxelShell()
    {
        var cube = TestMeshFactory.CreateCubeMesh();
        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SurfaceOnly,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 8,
            DefaultBlockNamespace = "minecraft:stone"
        };

        var result = await _surfaceAlgorithm.ExecuteAsync(cube, null, settings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.VoxelGrid);
        Assert.True(result.VoxelGrid.GetVoxelCount() > 0);
        // A surface shell of 8x8x8 cube should have fewer voxels than 8^3 = 512
        Assert.True(result.VoxelGrid.GetVoxelCount() < 512);
        Assert.True(result.Statistics.TotalTrianglesProcessed == 12);
    }

    [Fact]
    public async Task ExecuteAsync_SinglePlane_RasterizesIntersectingCells()
    {
        var plane = TestMeshFactory.CreatePlaneMesh();
        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SurfaceOnly,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 16,
            DefaultBlockNamespace = "minecraft:stone"
        };

        var result = await _surfaceAlgorithm.ExecuteAsync(plane, null, settings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.VoxelGrid);
        Assert.True(result.VoxelGrid.GetVoxelCount() > 0);
    }
}

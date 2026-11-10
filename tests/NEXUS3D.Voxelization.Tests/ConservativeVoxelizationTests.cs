using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Domain.Models;
using NEXUS3D.Voxelization.Engine.Algorithms;
using Xunit;

namespace NEXUS3D.Voxelization.Tests;

public class ConservativeVoxelizationTests
{
    private readonly ConservativeVoxelization _conservativeAlgorithm = new();
    private readonly SurfaceVoxelization _surfaceAlgorithm = new();

    [Fact]
    public async Task ExecuteAsync_ThinPlane_GeneratesGreaterOrEqualVoxelsThanStandardSurface()
    {
        var plane = TestMeshFactory.CreatePlaneMesh();
        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.Conservative,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 16,
            DefaultBlockNamespace = "minecraft:stone"
        };

        var surfaceResult = await _surfaceAlgorithm.ExecuteAsync(plane, null, settings);
        var conservativeResult = await _conservativeAlgorithm.ExecuteAsync(plane, null, settings);

        Assert.True(conservativeResult.IsSuccess);
        Assert.NotNull(conservativeResult.VoxelGrid);
        Assert.True(surfaceResult.IsSuccess);
        Assert.NotNull(surfaceResult.VoxelGrid);

        // Conservative dilation should capture equal or more boundary cells than standard SAT
        Assert.True(conservativeResult.VoxelGrid.GetVoxelCount() >= surfaceResult.VoxelGrid.GetVoxelCount());
    }
}

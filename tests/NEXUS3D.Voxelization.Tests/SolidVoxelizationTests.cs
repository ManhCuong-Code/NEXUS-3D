using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Domain.Models;
using NEXUS3D.Voxelization.Engine.Algorithms;
using Xunit;

namespace NEXUS3D.Voxelization.Tests;

public class SolidVoxelizationTests
{
    private readonly SolidVoxelization _solidAlgorithm = new();
    private readonly SurfaceVoxelization _surfaceAlgorithm = new();

    [Fact]
    public async Task ExecuteAsync_WatertightCubeMesh_FillsInteriorCellsSolid()
    {
        var cube = TestMeshFactory.CreateCubeMesh();
        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SolidVolume,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 8,
            DefaultBlockNamespace = "minecraft:stone"
        };

        var surfaceResult = await _surfaceAlgorithm.ExecuteAsync(cube, null, settings);
        var solidResult = await _solidAlgorithm.ExecuteAsync(cube, null, settings);

        Assert.True(solidResult.IsSuccess);
        Assert.NotNull(solidResult.VoxelGrid);
        Assert.True(surfaceResult.IsSuccess);
        Assert.NotNull(surfaceResult.VoxelGrid);

        // Solid voxel count MUST be strictly greater than surface shell voxel count for 8x8x8 cube
        Assert.True(solidResult.VoxelGrid.GetVoxelCount() > surfaceResult.VoxelGrid.GetVoxelCount(),
            $"Expected Solid voxels ({solidResult.VoxelGrid.GetVoxelCount()}) > Surface voxels ({surfaceResult.VoxelGrid.GetVoxelCount()})");
    }

    [Fact]
    public async Task ExecuteAsync_OpenMesh_GeneratesWarningForNonManifoldMesh()
    {
        var openCube = TestMeshFactory.CreateOpenCubeMesh();
        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SolidVolume,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 8,
            DefaultBlockNamespace = "minecraft:stone"
        };

        var result = await _solidAlgorithm.ExecuteAsync(openCube, null, settings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.VoxelGrid);
        // Expect a warning because mesh is open/non-manifold
        Assert.Contains(result.Warnings, w => w.Code == "VOX_OPEN_MESH");
    }
}

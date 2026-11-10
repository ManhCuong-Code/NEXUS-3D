using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Engine.Services;
using NEXUS3D.Domain.Voxels;
using Xunit;

namespace NEXUS3D.Conversion.Tests;

public sealed class VoxelToMeshConverterTests
{
    private readonly VoxelToMeshConverter _converter = new(NullLogger<VoxelToMeshConverter>.Instance);

    [Fact]
    public void Convert_SingleVoxel_SimpleBlockQuads_GeneratesSixQuads()
    {
        var grid = new VoxelGrid("SingleVoxel");
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 0));

        var result = _converter.ConvertVoxelGridToMesh(grid, new VoxelToMeshOptions(Algorithm: MeshingAlgorithm.SimpleBlockQuads));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(6, result.Value.Faces.Count);
        Assert.Equal(24, result.Value.Vertices.Count);
    }

    [Fact]
    public void Convert_TwoAdjacentVoxels_ExposedFaceCulling_CullsInternalFaces()
    {
        var grid = new VoxelGrid("AdjacentVoxels");
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 0));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 0), 0)); // Sharing X face

        var result = _converter.ConvertVoxelGridToMesh(grid, new VoxelToMeshOptions(Algorithm: MeshingAlgorithm.ExposedFaceCulling));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        // Total faces = (6 + 6) - 2 shared internal faces = 10 faces
        Assert.Equal(10, result.Value.Faces.Count);
    }

    [Fact]
    public void Convert_FourCoplanarVoxels_GreedyMeshing_MergesIntoSingleQuad()
    {
        var grid = new VoxelGrid("FlatPlane");
        // 2x2 coplanar voxels on Z=0
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 0));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 0), 0));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 1, 0), 0));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 1, 0), 0));

        var result = _converter.ConvertVoxelGridToMesh(grid, new VoxelToMeshOptions(Algorithm: MeshingAlgorithm.GreedyMeshing));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        // Simple quads would be 4 * 6 = 24 quads. Greedy meshing merges front/back to 1 quad each + 8 edge quads = 10 quads!
        Assert.True(result.Value.Faces.Count <= 10, $"Expected <= 10 faces from greedy meshing, actual: {result.Value.Faces.Count}");
    }
}

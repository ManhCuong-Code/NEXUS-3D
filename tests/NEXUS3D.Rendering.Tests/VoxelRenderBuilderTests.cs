using System.Numerics;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class VoxelRenderBuilderTests
{
    private readonly VoxelRenderBuilder _builder = new();

    [Fact]
    public void BuildVoxelChunk_SingleVoxelRendersSixFaces()
    {
        var grid = new VoxelGrid();
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 1, new BlockState("minecraft", "stone")));

        var chunk = _builder.Build(grid, enableCulling: true);

        Assert.Equal(1, chunk.VoxelCount);
        Assert.NotNull(chunk.Mesh);
        // 6 exposed faces * 4 vertices = 24 vertices
        Assert.Equal(24, chunk.Mesh.Positions.Length);
        // 6 exposed faces * 2 triangles * 3 indices = 36 indices
        Assert.Equal(36, chunk.Mesh.Indices.Length);
        Assert.Equal(24, chunk.Mesh.Colors.Length);
    }

    [Fact]
    public void BuildVoxelChunk_CullsSharedInternalFaces()
    {
        var grid = new VoxelGrid();
        // Two adjacent voxels along X
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 1, new BlockState("minecraft", "stone")));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 0), 1, new BlockState("minecraft", "stone")));

        var chunk = _builder.Build(grid, enableCulling: true);

        Assert.Equal(2, chunk.VoxelCount);
        Assert.NotNull(chunk.Mesh);
        // 2 separate cubes would have 12 faces. With 1 shared face pair culled, total exposed faces = 10.
        // 10 faces * 4 vertices = 40 vertices.
        Assert.Equal(40, chunk.Mesh.Positions.Length);
        // 10 faces * 6 indices = 60 indices.
        Assert.Equal(60, chunk.Mesh.Indices.Length);
    }

    [Fact]
    public void BuildSpatialChunks_PartitionsIntoChunks()
    {
        var grid = new VoxelGrid();
        // Voxel in chunk (0, 0, 0)
        grid.SetVoxel(new Voxel(new VoxelPosition(2, 2, 2), 1, new BlockState("minecraft", "stone")));
        // Voxel in chunk (1, 0, 0)
        grid.SetVoxel(new Voxel(new VoxelPosition(20, 2, 2), 1, new BlockState("minecraft", "dirt")));

        var chunks = _builder.BuildSpatialChunks(grid, chunkSize: 16);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(1, chunks[0].VoxelCount);
        Assert.Equal(1, chunks[1].VoxelCount);
    }

    [Fact]
    public void BuildVoxelChunk_MultiBlockTypes_GeneratesDistinctSubMeshesWithBlockMaterialIds()
    {
        var grid = new VoxelGrid();
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 1, new BlockState("minecraft", "stone")));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 1, 0), 2, new BlockState("minecraft", "dirt")));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 2, 0), 3, new BlockState("minecraft", "glass")));

        var chunk = _builder.Build(grid, enableCulling: true);

        Assert.Equal(3, chunk.VoxelCount);
        Assert.Equal(3, chunk.SubMeshes.Count);

        var matIds = chunk.SubMeshes.Select(s => s.MaterialId).ToHashSet();
        Assert.Contains("block:minecraft:stone", matIds);
        Assert.Contains("block:minecraft:dirt", matIds);
        Assert.Contains("block:minecraft:glass", matIds);

        foreach (var submesh in chunk.SubMeshes)
        {
            Assert.True(submesh.Positions.Length > 0);
            Assert.True(submesh.Indices.Length > 0);
            Assert.True(submesh.Colors.Length > 0);
        }
    }
}

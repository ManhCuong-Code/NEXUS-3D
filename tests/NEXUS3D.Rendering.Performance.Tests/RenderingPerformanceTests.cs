using System.Diagnostics;
using System.Numerics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Performance.Tests;

public sealed class RenderingPerformanceTests
{
    [Fact]
    public void LargeMeshRenderingBenchmark_Processes25kTrianglesWithinTimeLimit()
    {
        // Generate a grid mesh of 100x125 quads = 25,000 triangles
        int width = 100;
        int height = 125;
        var vertices = new List<Vertex>((width + 1) * (height + 1));

        for (int y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                vertices.Add(new Vertex(new Vector3D(x * 0.1, Math.Sin(x * 0.1) * Math.Cos(y * 0.1), y * 0.1)));
            }
        }

        var faces = new List<Face>(width * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i0 = y * (width + 1) + x;
                int i1 = i0 + 1;
                int i2 = i0 + (width + 1) + 1;
                int i3 = i0 + (width + 1);
                faces.Add(new Face([i0, i1, i2, i3]));
            }
        }

        var meshData = new MeshData
        {
            Name = "DenseMesh",
            Vertices = vertices,
            Faces = faces
        };

        var builder = new MeshRenderBuilder();
        var sw = Stopwatch.StartNew();

        var renderMesh = builder.Build(meshData);

        sw.Stop();

        Assert.Equal(25000, renderMesh.TriangleCount);
        Assert.Equal(25000 * 3, renderMesh.Indices.Length);
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Mesh build took {sw.ElapsedMilliseconds}ms, exceeding 1000ms target.");
    }

    [Fact]
    public void LargeVoxelGridRenderingBenchmark_Culls32kSolidVoxelsEfficiently()
    {
        // 32x32x32 solid cube = 32,768 voxels
        var grid = new VoxelGrid();
        for (int z = 0; z < 32; z++)
        {
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    grid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), 1, new BlockState("minecraft", "stone")));
                }
            }
        }

        var builder = new VoxelRenderBuilder();
        var sw = Stopwatch.StartNew();

        var chunk = builder.Build(grid, enableCulling: true);

        sw.Stop();

        Assert.Equal(32768, chunk.VoxelCount);
        Assert.NotNull(chunk.Mesh);

        // A 32x32x32 cube has 6 * 32 * 32 = 6,144 exposed faces (out of 196,608 total faces)
        // 6,144 faces * 4 vertices = 24,576 vertices
        Assert.Equal(24576, chunk.Mesh.Positions.Length);
        Assert.True(sw.ElapsedMilliseconds < 1500, $"Voxel culling took {sw.ElapsedMilliseconds}ms, exceeding 1500ms target.");
    }

    [Fact]
    public void SpatialChunkingPerformanceBenchmark_Partitions32kVoxelsWithinTimeLimit()
    {
        var grid = new VoxelGrid();
        for (int z = 0; z < 32; z++)
        {
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    grid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), 1, new BlockState("minecraft", "stone")));
                }
            }
        }

        var builder = new VoxelRenderBuilder();
        var sw = Stopwatch.StartNew();

        var chunks = builder.BuildSpatialChunks(grid, chunkSize: 16);

        sw.Stop();

        // 32x32x32 divided by 16x16x16 = 8 chunks
        Assert.Equal(8, chunks.Count);
        Assert.True(sw.ElapsedMilliseconds < 1500, $"Spatial chunking took {sw.ElapsedMilliseconds}ms, exceeding 1500ms target.");
    }
}


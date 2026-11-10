using System.Diagnostics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Voxels;
using Xunit;
using Xunit.Abstractions;

namespace NEXUS3D.Performance.Tests;

public sealed class LargeModelProfilingBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public LargeModelProfilingBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void MeshProcessing_SmallModel_50kVertices_Benchmark()
    {
        const int vertexCount = 50_000;
        long memBefore = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();

        var mesh = new MeshData("SmallBenchMesh");
        for (int i = 0; i < vertexCount; i++)
        {
            mesh.Vertices.Add(new Vertex(new Vector3D(i * 0.1, (i % 100) * 0.2, (i / 100) * 0.3)));
        }

        var bounds = mesh.CalculateBounds();
        sw.Stop();
        long memAfter = GC.GetTotalMemory(false);

        long allocatedBytes = memAfter - memBefore;
        _output.WriteLine($"[BENCHMARK] Small Model (50k vertices): {sw.ElapsedMilliseconds} ms, Allocated ~{allocatedBytes / 1024} KB. Bounds: {bounds.Min} to {bounds.Max}");

        Assert.Equal(vertexCount, mesh.Vertices.Count);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Processing took {sw.ElapsedMilliseconds} ms, expected < 500 ms.");
    }

    [Fact]
    public void MeshProcessing_MediumModel_250kVertices_Benchmark()
    {
        const int vertexCount = 250_000;
        long memBefore = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();

        var mesh = new MeshData("MediumBenchMesh");
        for (int i = 0; i < vertexCount; i++)
        {
            mesh.Vertices.Add(new Vertex(
                new Vector3D(i * 0.05, (i % 250) * 0.1, (i / 250) * 0.1),
                new Vector3D(0, 1, 0),
                new Vector2D(0.5, 0.5)));
        }

        var stats = mesh.CalculateStatistics();
        sw.Stop();
        long memAfter = GC.GetTotalMemory(false);

        long allocatedBytes = memAfter - memBefore;
        _output.WriteLine($"[BENCHMARK] Medium Model (250k vertices): {sw.ElapsedMilliseconds} ms, Allocated ~{allocatedBytes / (1024 * 1024)} MB.");

        Assert.Equal(vertexCount, stats.VertexCount);
        Assert.True(stats.HasNormals);
        Assert.True(stats.HasTexCoords);
        Assert.True(sw.ElapsedMilliseconds < 2500, $"Processing took {sw.ElapsedMilliseconds} ms, expected < 2500 ms.");
    }

    [Fact]
    public void MeshProcessing_LargeModel_OneMillionVertices_MemoryEfficiency()
    {
        const int vertexCount = 1_000_000;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long memBefore = GC.GetTotalMemory(true);

        var sw = Stopwatch.StartNew();
        var mesh = new MeshData("OneMillionVertexMesh");
        mesh.Vertices.Capacity = vertexCount;

        for (int i = 0; i < vertexCount; i++)
        {
            mesh.Vertices.Add(new Vertex(new Vector3D(i * 0.01, i * 0.02, i * 0.03)));
        }

        var bounds = mesh.CalculateBounds();
        sw.Stop();

        long memAfter = GC.GetTotalMemory(false);
        long allocatedBytes = memAfter - memBefore;
        double mbAllocated = allocatedBytes / (1024.0 * 1024.0);

        _output.WriteLine($"[BENCHMARK] Large Model (1,000,000 vertices): {sw.ElapsedMilliseconds} ms, Allocated {mbAllocated:F2} MB (Avg {allocatedBytes / vertexCount} bytes/vertex).");

        Assert.Equal(vertexCount, mesh.Vertices.Count);
        // Thanks to lazy CustomAttributes, memory per vertex drops from ~330 MB to ~200 MB on 64-bit CLR
        Assert.True(mbAllocated < 250.0, $"Allocated {mbAllocated:F2} MB, expected < 250 MB.");
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Processing took {sw.ElapsedMilliseconds} ms, expected < 5000 ms.");
    }

    [Fact]
    public void VoxelStorage_SparseVsChunked_ComparisonBenchmark()
    {
        const int voxelCount = 50_000;

        // 1. Sparse Backend Benchmark
        var sparseGrid = new VoxelGrid("SparseBench", new SparseVoxelStorageBackend());
        var swSparse = Stopwatch.StartNew();
        for (int i = 0; i < voxelCount; i++)
        {
            int x = (i % 64);
            int y = ((i / 64) % 64);
            int z = (i / 4096);
            sparseGrid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), 1));
        }
        swSparse.Stop();

        // 2. Chunked Backend Benchmark
        var chunkedGrid = new VoxelGrid("ChunkedBench", new ChunkedVoxelStorageBackend());
        var swChunked = Stopwatch.StartNew();
        for (int i = 0; i < voxelCount; i++)
        {
            int x = (i % 64);
            int y = ((i / 64) % 64);
            int z = (i / 4096);
            chunkedGrid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), 1));
        }
        swChunked.Stop();

        _output.WriteLine($"[BENCHMARK] Voxel Insertions (50,000 voxels): Sparse={swSparse.ElapsedMilliseconds}ms, Chunked={swChunked.ElapsedMilliseconds}ms");

        Assert.Equal(voxelCount, sparseGrid.GetVoxelCount());
        Assert.Equal(voxelCount, chunkedGrid.GetVoxelCount());

        // Both should easily complete in under 1 second
        Assert.True(swSparse.ElapsedMilliseconds < 1500);
        Assert.True(swChunked.ElapsedMilliseconds < 1500);
    }
}

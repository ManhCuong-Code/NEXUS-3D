using System.Diagnostics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Domain.Models;
using NEXUS3D.Voxelization.Engine.Algorithms;
using NEXUS3D.Voxelization.Engine.Conversion;
using Xunit;
using Xunit.Abstractions;

namespace NEXUS3D.Voxelization.Performance.Tests;

public class VoxelizationPerformanceBenchmarks
{
    private readonly ITestOutputHelper _output;
    private readonly SurfaceVoxelization _surfaceVoxelizer = new();
    private readonly SolidVoxelization _solidVoxelizer = new();
    private readonly VoxelToCuboidConverter _cuboidConverter = new();

    public VoxelizationPerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Benchmark_SurfaceVoxelization_25kTriangles_CompletesUnder1500Ms()
    {
        // Generate a dense mesh with ~25,000 triangles (e.g. 112 x 112 grid of quads = 25,088 triangles)
        int gridResolution = 112;
        var mesh = new MeshData("DenseTerrainMesh");

        for (int y = 0; y <= gridResolution; y++)
        {
            for (int x = 0; x <= gridResolution; x++)
            {
                double px = (double)x / gridResolution * 100.0;
                double pz = (double)y / gridResolution * 100.0;
                double py = Math.Sin(px * 0.1) * Math.Cos(pz * 0.1) * 10.0;
                mesh.Vertices.Add(new Vertex(new Vector3D(px, py, pz)));
            }
        }

        int stride = gridResolution + 1;
        for (int y = 0; y < gridResolution; y++)
        {
            for (int x = 0; x < gridResolution; x++)
            {
                int i00 = y * stride + x;
                int i10 = y * stride + (x + 1);
                int i01 = (y + 1) * stride + x;
                int i11 = (y + 1) * stride + (x + 1);

                mesh.Faces.Add(new Face([i00, i01, i10]));
                mesh.Faces.Add(new Face([i10, i01, i11]));
            }
        }

        int triangleCount = mesh.Faces.Count;
        Assert.True(triangleCount >= 25_000, $"Expected >= 25,000 triangles, generated {triangleCount}");

        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SurfaceOnly,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 64,
            DefaultBlockNamespace = "minecraft:stone"
        };

        // Warmup JIT
        _ = await _surfaceVoxelizer.ExecuteAsync(mesh, null, settings);

        // Benchmark
        var sw = Stopwatch.StartNew();
        var result = await _surfaceVoxelizer.ExecuteAsync(mesh, null, settings);
        sw.Stop();

        _output.WriteLine($"[BENCHMARK] Surface Voxelization: {triangleCount:N0} triangles -> {result.VoxelGrid?.GetVoxelCount():N0} voxels in {sw.ElapsedMilliseconds} ms");

        Assert.True(result.IsSuccess);
        Assert.True(sw.ElapsedMilliseconds < 1500, $"Surface voxelization took {sw.ElapsedMilliseconds} ms (target: < 1500 ms)");
    }

    [Fact]
    public async Task Benchmark_SolidVoxelization_64Cube_CompletesUnder2000Ms()
    {
        // 12-triangle watertight cube voxelized at 64x64x64 resolution (up to 262,144 voxels)
        var cube = new MeshData("WatertightCube64");
        cube.Vertices.Add(new Vertex(new Vector3D(-16.0, -16.0, -16.0)));
        cube.Vertices.Add(new Vertex(new Vector3D( 16.0, -16.0, -16.0)));
        cube.Vertices.Add(new Vertex(new Vector3D( 16.0,  16.0, -16.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(-16.0,  16.0, -16.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(-16.0, -16.0,  16.0)));
        cube.Vertices.Add(new Vertex(new Vector3D( 16.0, -16.0,  16.0)));
        cube.Vertices.Add(new Vertex(new Vector3D( 16.0,  16.0,  16.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(-16.0,  16.0,  16.0)));

        AddQuad(cube, 0, 3, 2, 1);
        AddQuad(cube, 5, 6, 7, 4);
        AddQuad(cube, 4, 7, 3, 0);
        AddQuad(cube, 1, 2, 6, 5);
        AddQuad(cube, 3, 7, 6, 2);
        AddQuad(cube, 4, 0, 1, 5);

        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SolidVolume,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 64,
            DefaultBlockNamespace = "minecraft:stone"
        };

        // Warmup
        _ = await _solidVoxelizer.ExecuteAsync(cube, null, settings);

        // Benchmark
        var sw = Stopwatch.StartNew();
        var result = await _solidVoxelizer.ExecuteAsync(cube, null, settings);
        sw.Stop();

        _output.WriteLine($"[BENCHMARK] Solid Voxelization: {result.VoxelGrid?.GetVoxelCount():N0} voxels in {sw.ElapsedMilliseconds} ms");

        Assert.True(result.IsSuccess);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Solid flood fill took {sw.ElapsedMilliseconds} ms (target: < 2000 ms)");
    }

    [Fact]
    public async Task Benchmark_GreedyCuboidMerging_32kVoxels_CompletesUnder1000MsAndReducesOver80Percent()
    {
        // Create 32x32x32 = 32,768 solid voxels
        var grid = new VoxelGrid("BenchmarkGrid32k");
        var state = new BlockState("minecraft", "stone");
        int pal = grid.Palette.GetOrAdd(state);

        for (int z = 0; z < 32; z++)
        {
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    grid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), pal, state));
                }
            }
        }

        Assert.Equal(32_768, grid.GetVoxelCount());

        // Warmup
        _ = await _cuboidConverter.ConvertToCuboidsAsync(grid);

        // Benchmark
        var sw = Stopwatch.StartNew();
        var optResult = await _cuboidConverter.ConvertToCuboidsAsync(grid);
        sw.Stop();

        _output.WriteLine($"[BENCHMARK] Greedy Cuboid: {optResult.OriginalVoxelCount:N0} voxels -> {optResult.CuboidCount:N0} cuboids ({optResult.ReductionPercentage:F2}% reduction) in {sw.ElapsedMilliseconds} ms");

        Assert.True(sw.ElapsedMilliseconds < 1000, $"Greedy cuboid merging took {sw.ElapsedMilliseconds} ms (target: < 1000 ms)");
        Assert.True(optResult.ReductionPercentage >= 80.0, $"Expected >= 80% reduction, achieved {optResult.ReductionPercentage:F2}%");
    }

    private static void AddQuad(MeshData mesh, int i0, int i1, int i2, int i3)
    {
        mesh.Faces.Add(new Face([i0, i1, i2]));
        mesh.Faces.Add(new Face([i0, i2, i3]));
    }
}

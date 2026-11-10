using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Engine.Services;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Voxels;
using Xunit;

namespace NEXUS3D.Conversion.Performance.Tests;

public sealed class ConversionPerformanceBenchmarks
{
    private readonly VoxelToMeshConverter _voxelToMesh = new(NullLogger<VoxelToMeshConverter>.Instance);
    private readonly MeshToVoxelConverter _meshToVoxel = new(new PolygonTriangulator(), NullLogger<MeshToVoxelConverter>.Instance);

    [Fact]
    public void Benchmark_VoxelToMesh_50k_Voxels_GreedyMeshing_CompletesUnder5Seconds()
    {
        var grid = new VoxelGrid("HighDensity50k");
        int count = 0;

        // 50 x 50 x 20 = 50,000 voxels
        for (int x = 0; x < 50; x++)
        {
            for (int y = 0; y < 20; y++)
            {
                for (int z = 0; z < 50; z++)
                {
                    grid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), (x + y + z) % 4));
                    count++;
                }
            }
        }

        var sw = Stopwatch.StartNew();
        var result = _voxelToMesh.ConvertVoxelGridToMesh(grid, new VoxelToMeshOptions(Algorithm: MeshingAlgorithm.GreedyMeshing));
        sw.Stop();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Expected < 5s for 50k voxels greedy meshing, actual: {sw.Elapsed}");
    }

    [Fact]
    public void Benchmark_MeshToVoxel_10k_Triangles_CompletesUnder5Seconds()
    {
        var mesh = new MeshData("HighPolySphere", MeshTopology.Triangles);
        int rings = 50;
        int sectors = 100;
        double radius = 10.0;

        // Generate sphere vertices (approx 5,000 vertices, 10,000 triangles)
        for (int r = 0; r <= rings; r++)
        {
            double phi = Math.PI * r / rings;
            for (int s = 0; s <= sectors; s++)
            {
                double theta = 2.0 * Math.PI * s / sectors;
                double x = radius * Math.Sin(phi) * Math.Cos(theta);
                double y = radius * Math.Cos(phi);
                double z = radius * Math.Sin(phi) * Math.Sin(theta);
                mesh.Vertices.Add(new Vertex(new Vector3D(x, y, z)));
            }
        }

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < sectors; s++)
            {
                int first = r * (sectors + 1) + s;
                int second = first + sectors + 1;

                mesh.Faces.Add(new Face([first, second, first + 1]));
                mesh.Faces.Add(new Face([second, second + 1, first + 1]));
            }
        }

        Assert.True(mesh.Faces.Count >= 10000, $"Expected >= 10,000 faces, actual: {mesh.Faces.Count}");

        var sw = Stopwatch.StartNew();
        var result = _meshToVoxel.ConvertMeshToVoxelGrid(mesh, null, new MeshToVoxelOptions(
            VoxelSize: 1.0,
            Mode: VoxelizationMode.SurfaceOnly));
        sw.Stop();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Expected < 5s for 10k triangles voxelization, actual: {sw.Elapsed}");
    }
}

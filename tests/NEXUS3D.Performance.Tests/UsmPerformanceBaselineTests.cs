using System.Diagnostics;
using NEXUS3D.Domain.Factories;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Infrastructure.Serialization.Json;

namespace NEXUS3D.Performance.Tests;

public sealed class UsmPerformanceBaselineTests
{
    [Fact]
    public void SceneGraph_100000Nodes_TraverseValidateAndClone_ShouldCompleteWithinBudget()
    {
        const int targetNodeCount = 100_000;
        var sceneFactory = new SceneFactory();
        Scene3D scene = sceneFactory.Create("Enterprise100kScene");

        var sw = Stopwatch.StartNew();

        // Build a balanced multi-branch hierarchy with 100,000 total nodes (including Root)
        var parents = new List<SceneNode> { scene.Graph.Root };
        int created = 1;
        int parentCursor = 0;

        while (created < targetNodeCount)
        {
            SceneNode parent = parents[parentCursor++];
            for (int b = 0; b < 16 && created < targetNodeCount; b++)
            {
                var child = new SceneNode($"N_{created}");
                scene.Graph.AddNode(parent, child);
                parents.Add(child);
                created++;
            }
        }

        long buildMs = sw.ElapsedMilliseconds;
        Assert.Equal(targetNodeCount, scene.Graph.Nodes.Count);

        sw.Restart();
        int traversedCount = scene.Graph.Traverse().Count();
        long traverseMs = sw.ElapsedMilliseconds;
        Assert.Equal(targetNodeCount, traversedCount);

        sw.Restart();
        var validation = scene.Validate();
        long validateMs = sw.ElapsedMilliseconds;
        Assert.True(validation.IsValid);

        sw.Restart();
        Scene3D cloned = scene.DeepClone();
        long cloneMs = sw.ElapsedMilliseconds;
        Assert.Equal(targetNodeCount, cloned.Graph.Nodes.Count);

        Assert.True(buildMs < 10_000, $"100k node build took {buildMs}ms");
        Assert.True(traverseMs < 2_000, $"100k node traversal took {traverseMs}ms");
        Assert.True(validateMs < 5_000, $"100k node validation took {validateMs}ms");
        Assert.True(cloneMs < 5_000, $"100k node deep clone took {cloneMs}ms");
    }

    [Fact]
    public void VoxelGrid_100000Voxels_SparseStorageAndValidation_ShouldCompleteWithinBudget()
    {
        const int voxelCount = 100_000;
        var grid = new VoxelGrid("LargeVoxelGrid", new SparseVoxelStorageBackend(voxelCount));
        int stoneIdx = grid.Palette.GetOrAdd(new BlockState("minecraft", "stone"));

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < voxelCount; i++)
        {
            int x = i % 100;
            int z = (i / 100) % 100;
            int y = i / 10_000;
            grid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), stoneIdx));
        }
        long insertMs = sw.ElapsedMilliseconds;

        Assert.Equal(voxelCount, grid.GetVoxelCount());

        sw.Restart();
        var validation = grid.Validate();
        long validateMs = sw.ElapsedMilliseconds;
        Assert.True(validation.IsValid);

        Assert.True(insertMs < 5_000, $"100k voxel insertion took {insertMs}ms");
        Assert.True(validateMs < 5_000, $"100k voxel validation took {validateMs}ms");
    }

    [Fact]
    public void MeshData_50000Vertices_BoundsStatisticsValidateAndSerialize_ShouldCompleteWithinBudget()
    {
        const int vertexCount = 50_001; // divisible by 3 -> 16,667 triangles
        var mesh = new MeshData("HighPolyMesh", MeshTopology.Triangles);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < vertexCount; i += 3)
        {
            double offset = i * 0.01;
            mesh.Vertices.Add(new Vertex(new Vector3D(offset, 0, 0), Vector3D.UnitY, new Vector2D(0, 0)));
            mesh.Vertices.Add(new Vertex(new Vector3D(offset + 1, 0, 0), Vector3D.UnitY, new Vector2D(1, 0)));
            mesh.Vertices.Add(new Vertex(new Vector3D(offset, 0, 1), Vector3D.UnitY, new Vector2D(0, 1)));
            mesh.Faces.Add(new Face([i, i + 1, i + 2]));
        }

        MeshStatistics stats = mesh.CalculateStatistics();
        Assert.Equal(vertexCount, stats.VertexCount);
        Assert.Equal(vertexCount / 3, stats.FaceCount);

        var validation = mesh.Validate();
        Assert.True(validation.IsValid);

        var scene = new SceneFactory().Create("MeshPerfScene");
        scene.Graph.AddNode(scene.Graph.Root, new SceneNodeFactory().CreateMeshNode("MeshNode", mesh));

        var serializer = new SceneJsonSerializer();
        var serRes = serializer.SerializeToString(scene);
        Assert.True(serRes.IsSuccess);

        long totalMs = sw.ElapsedMilliseconds;
        Assert.True(totalMs < 10_000, $"50k vertex mesh pipeline took {totalMs}ms");
    }
}

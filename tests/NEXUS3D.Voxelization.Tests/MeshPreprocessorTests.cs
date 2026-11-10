using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Voxelization.Engine.Processing;
using Xunit;

namespace NEXUS3D.Voxelization.Tests;

public class MeshPreprocessorTests
{
    private readonly MeshPreprocessor _preprocessor = new();

    [Fact]
    public void Preprocess_WatertightCubeMesh_ReturnsIsClosedTrueAndNoWarnings()
    {
        var cube = TestMeshFactory.CreateCubeMesh();

        var result = _preprocessor.Preprocess(cube);

        Assert.True(result.IsClosedManifold);
        Assert.Equal(12, result.Triangles.Count);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "DEGENERATE_TRIANGLES_FILTERED");
    }

    [Fact]
    public void Preprocess_OpenCubeMesh_ReturnsIsClosedFalseAndIssuesWarning()
    {
        var openCube = TestMeshFactory.CreateOpenCubeMesh();

        var result = _preprocessor.Preprocess(openCube);

        Assert.False(result.IsClosedManifold);
        Assert.Equal(10, result.Triangles.Count);
        Assert.Contains(result.Warnings, w => w.Code == "VOX_OPEN_MESH");
    }

    [Fact]
    public void Preprocess_DegenerateTriangle_IsFilteredOut()
    {
        var mesh = new MeshData("DegenerateTest");
        mesh.Vertices.Add(new Vertex(new Vector3D(0.0, 0.0, 0.0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(1.0, 0.0, 0.0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0.0, 1.0, 0.0)));
        // Degenerate collinear vertex
        mesh.Vertices.Add(new Vertex(new Vector3D(2.0, 0.0, 0.0)));

        // Valid triangle
        mesh.Faces.Add(new Face([0, 1, 2]));

        // Degenerate triangle (points 0, 1, 3 are collinear along X)
        mesh.Faces.Add(new Face([0, 1, 3]));

        var result = _preprocessor.Preprocess(mesh);

        Assert.Single(result.Triangles);
        Assert.Contains(result.Warnings, w => w.Code == "VOX_DEGENERATE_TRIANGLES");
    }
}

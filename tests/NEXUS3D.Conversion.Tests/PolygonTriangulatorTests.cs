using NEXUS3D.Conversion.Engine.Services;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using Xunit;

namespace NEXUS3D.Conversion.Tests;

public sealed class PolygonTriangulatorTests
{
    private readonly PolygonTriangulator _triangulator = new();

    [Fact]
    public void TriangulateFace_Triangle_ReturnsUnchanged()
    {
        var face = new Face([0, 1, 2]);
        var vertices = new List<Vertex>
        {
            new(new Vector3D(0, 0, 0)),
            new(new Vector3D(1, 0, 0)),
            new(new Vector3D(0, 1, 0))
        };

        var result = _triangulator.TriangulateFace(face, vertices);

        Assert.Single(result);
        Assert.Equal([0, 1, 2], result[0].VertexIndices);
    }

    [Fact]
    public void TriangulateFace_Quad_ProducesTwoTriangles()
    {
        var face = new Face([0, 1, 2, 3]);
        var vertices = new List<Vertex>
        {
            new(new Vector3D(0, 0, 0)),
            new(new Vector3D(1, 0, 0)),
            new(new Vector3D(1, 1, 0)),
            new(new Vector3D(0, 1, 0))
        };

        var result = _triangulator.TriangulateFace(face, vertices);

        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Equal(3, f.VertexIndices.Count));
    }

    [Fact]
    public void TriangulateMesh_ConvertsAllFacesToTriangles()
    {
        var mesh = new MeshData("QuadMesh", MeshTopology.Polygons);
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(1, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(1, 1, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 1, 0)));
        mesh.Faces.Add(new Face([0, 1, 2, 3]));

        var triMesh = _triangulator.TriangulateMesh(mesh);

        Assert.Equal(MeshTopology.Triangles, triMesh.Topology);
        Assert.Equal(2, triMesh.Faces.Count);
        Assert.All(triMesh.Faces, f => Assert.True(f.IsTriangle));
    }
}

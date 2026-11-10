using System.Numerics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class MeshRenderBuilderTests
{
    private readonly MeshRenderBuilder _builder = new();

    [Fact]
    public void BuildMesh_TriangulatesQuadFaceProperly()
    {
        var meshData = new MeshData
        {
            Name = "QuadMesh",
            Vertices =
            [
                new Vertex(new Vector3D(0, 0, 0)),
                new Vertex(new Vector3D(1, 0, 0)),
                new Vertex(new Vector3D(1, 1, 0)),
                new Vertex(new Vector3D(0, 1, 0))
            ],
            Faces =
            [
                new Face([0, 1, 2, 3])
            ]
        };

        var renderMesh = _builder.Build(meshData);

        Assert.Equal("QuadMesh", renderMesh.Name);
        Assert.Equal(4, renderMesh.Positions.Length);
        // Quad becomes 2 triangles -> 6 indices
        Assert.Equal(6, renderMesh.Indices.Length);
        Assert.Equal(0, renderMesh.Indices[0]);
        Assert.Equal(1, renderMesh.Indices[1]);
        Assert.Equal(2, renderMesh.Indices[2]);
        Assert.Equal(0, renderMesh.Indices[3]);
        Assert.Equal(2, renderMesh.Indices[4]);
        Assert.Equal(3, renderMesh.Indices[5]);
    }

    [Fact]
    public void BuildMesh_GeneratesNormalsWhenMissing()
    {
        var meshData = new MeshData
        {
            Vertices =
            [
                new Vertex(new Vector3D(0, 0, 0)),
                new Vertex(new Vector3D(1, 0, 0)),
                new Vertex(new Vector3D(0, 1, 0))
            ],
            Faces =
            [
                new Face([0, 1, 2])
            ]
        };

        var renderMesh = _builder.Build(meshData);

        Assert.Equal(3, renderMesh.Normals.Length);
        // Face is on XY plane, normal should be along Z axis
        Assert.True(MathF.Abs(renderMesh.Normals[0].Z) > 0.9f);
    }

    [Fact]
    public void BuildMesh_GeneratesWireframeIndices()
    {
        var meshData = new MeshData
        {
            Vertices =
            [
                new Vertex(new Vector3D(0, 0, 0)),
                new Vertex(new Vector3D(1, 0, 0)),
                new Vertex(new Vector3D(0, 1, 0))
            ],
            Faces =
            [
                new Face([0, 1, 2])
            ]
        };

        var renderMesh = _builder.Build(meshData);

        // 1 triangle = 3 edges = 6 indices in line list
        Assert.Equal(6, renderMesh.WireframeIndices.Length);
    }

    [Fact]
    public void BuildMesh_EmptyMeshReturnsEmptyRenderMesh()
    {
        var meshData = new MeshData { Name = "Empty" };

        var renderMesh = _builder.Build(meshData);

        Assert.Equal("Empty", renderMesh.Name);
        Assert.Empty(renderMesh.Positions);
        Assert.Empty(renderMesh.Indices);
    }
}

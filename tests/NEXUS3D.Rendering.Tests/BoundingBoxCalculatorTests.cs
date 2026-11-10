using System.Numerics;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class BoundingBoxCalculatorTests
{
    private readonly BoundingBoxCalculator _calc = new();

    [Fact]
    public void CalculateMeshBounds_ReturnsCorrectExtents()
    {
        var positions = new[]
        {
            new Vector3(-2, 0, 1),
            new Vector3(4, -3, 2),
            new Vector3(0, 5, -6)
        };

        var (min, max) = _calc.CalculateMeshBounds(positions, Matrix4x4.Identity);

        Assert.Equal(new Vector3(-2, -3, -6), min);
        Assert.Equal(new Vector3(4, 5, 2), max);
    }

    [Fact]
    public void CalculateMeshBounds_AppliesTransformMatrix()
    {
        var positions = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 1, 1)
        };

        var transform = Matrix4x4.CreateTranslation(new Vector3(10, 20, 30));
        var (min, max) = _calc.CalculateMeshBounds(positions, transform);

        Assert.Equal(new Vector3(10, 20, 30), min);
        Assert.Equal(new Vector3(11, 21, 31), max);
    }

    [Fact]
    public void CalculateNodeBounds_AggregatesMeshAndChildBounds()
    {
        var childMesh = new RenderMesh
        {
            Name = "ChildMesh",
            Positions = [new Vector3(0, 0, 0), new Vector3(1, 1, 1)],
            Normals = [Vector3.UnitY, Vector3.UnitY],
            Indices = [0, 1, 0],
            BoundsMin = new Vector3(0, 0, 0),
            BoundsMax = new Vector3(1, 1, 1)
        };

        var childNode = new RenderNode
        {
            Id = "child-1",
            Name = "Child",
            NodeType = RenderNodeType.Mesh,
            LocalTransform = Matrix4x4.CreateTranslation(0, 10, 0),
            WorldTransform = Matrix4x4.CreateTranslation(0, 10, 0)
        };
        childNode.Meshes.Add(childMesh);

        var parentMesh = new RenderMesh
        {
            Name = "ParentMesh",
            Positions = [new Vector3(-5, -5, -5), new Vector3(0, 0, 0)],
            Normals = [Vector3.UnitY, Vector3.UnitY],
            Indices = [0, 1, 0],
            BoundsMin = new Vector3(-5, -5, -5),
            BoundsMax = new Vector3(0, 0, 0)
        };

        var parentNode = new RenderNode
        {
            Id = "parent-1",
            Name = "Parent",
            NodeType = RenderNodeType.Group,
            LocalTransform = Matrix4x4.Identity
        };
        parentNode.Meshes.Add(parentMesh);
        parentNode.Children.Add(childNode);

        var (min, max) = _calc.CalculateNodeBounds(parentNode);

        Assert.Equal(-5f, min.X);
        Assert.Equal(-5f, min.Y);
        Assert.Equal(-5f, min.Z);
        Assert.True(max.Y >= 10f);
    }
}

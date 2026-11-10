using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Services;
using Xunit;

namespace NEXUS3D.Editor.Tests;

public sealed class TransformServiceTests
{
    [Fact]
    public void Translate_WorldSpace_UpdatesPosition()
    {
        var service = new TransformService();
        var node = new SceneNode("Node") { LocalTransform = new Transform3D(new Vector3D(1, 2, 3), QuaternionD.Identity, Vector3D.One) };

        service.Translate([node], new Vector3D(5, -1, 2), TransformSpace.World);

        Assert.Equal(new Vector3D(6, 1, 5), node.LocalTransform.Position);
    }

    [Fact]
    public void Translate_SnappingEnabled_SnapsDelta()
    {
        var service = new TransformService
        {
            SnappingEnabled = true,
            TranslationSnap = 1.0
        };
        var node = new SceneNode("Node") { LocalTransform = Transform3D.Identity() };

        service.Translate([node], new Vector3D(0.8, 1.2, 2.7), TransformSpace.World);

        Assert.Equal(new Vector3D(1.0, 1.0, 3.0), node.LocalTransform.Position);
    }

    [Fact]
    public void Rotate_AroundOriginPivot_UpdatesOrientationAndPosition()
    {
        var service = new TransformService();
        var node = new SceneNode("Node")
        {
            LocalTransform = new Transform3D(new Vector3D(10, 0, 0), QuaternionD.Identity, Vector3D.One)
        };

        // 90 degrees around Y axis: point (10, 0, 0) should rotate to (0, 0, -10)
        var rot90Y = QuaternionD.FromAxisAngle(Vector3D.UnitY, Math.PI / 2.0);
        service.Rotate([node], rot90Y, pivot: Vector3D.Zero, TransformSpace.World);

        Assert.True(Math.Abs(node.LocalTransform.Position.X) < 1e-4);
        Assert.True(Math.Abs(node.LocalTransform.Position.Y) < 1e-4);
        Assert.True(Math.Abs(node.LocalTransform.Position.Z - (-10.0)) < 1e-4);
    }

    [Fact]
    public void Scale_RelativePivot_ScalesDistanceAndLocalScale()
    {
        var service = new TransformService();
        var node = new SceneNode("Node")
        {
            LocalTransform = new Transform3D(new Vector3D(10, 0, 0), QuaternionD.Identity, new Vector3D(1, 1, 1))
        };

        service.Scale([node], new Vector3D(2, 2, 2), pivot: Vector3D.Zero);

        Assert.Equal(new Vector3D(20, 0, 0), node.LocalTransform.Position);
        Assert.Equal(new Vector3D(2, 2, 2), node.LocalTransform.Scale);
    }

    [Fact]
    public void Scale_ZeroScaleGuard_PreventsZero()
    {
        var service = new TransformService();
        var node = new SceneNode("Node") { LocalTransform = Transform3D.Identity() };

        service.Scale([node], new Vector3D(0, 0, 0), pivot: Vector3D.Zero);

        Assert.True(node.LocalTransform.Scale.X > 0);
        Assert.True(node.LocalTransform.Scale.Y > 0);
        Assert.True(node.LocalTransform.Scale.Z > 0);
    }

    [Fact]
    public void ResetTransform_RestoresIdentity()
    {
        var service = new TransformService();
        var node = new SceneNode("Node")
        {
            LocalTransform = new Transform3D(new Vector3D(5, 10, -3), QuaternionD.FromAxisAngle(Vector3D.UnitY, 1.2), new Vector3D(3, 4, 5))
        };

        service.ResetTransform([node]);

        Assert.Equal(Vector3D.Zero, node.LocalTransform.Position);
        Assert.Equal(QuaternionD.Identity, node.LocalTransform.Rotation);
        Assert.Equal(Vector3D.One, node.LocalTransform.Scale);
    }

    [Fact]
    public void ApplyTransform_BakesVerticesAndResetsLocalTransform()
    {
        var service = new TransformService();
        var mesh = new MeshData("Mesh");
        mesh.Vertices.Add(new Vertex(new Vector3D(1, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(2, 0, 0)));

        var node = new SceneNode("Node", new SceneObject("Obj") { Meshes = { mesh } })
        {
            LocalTransform = new Transform3D(new Vector3D(10, 0, 0), QuaternionD.Identity, new Vector3D(2, 1, 1))
        };

        service.ApplyTransform([node]);

        // Vertices should be (1*2 + 10 = 12), (2*2 + 10 = 14)
        Assert.Equal(new Vector3D(12, 0, 0), mesh.Vertices[0].Position);
        Assert.Equal(new Vector3D(14, 0, 0), mesh.Vertices[1].Position);

        // Local transform reset to identity
        Assert.Equal(Vector3D.Zero, node.LocalTransform.Position);
        Assert.Equal(QuaternionD.Identity, node.LocalTransform.Rotation);
        Assert.Equal(Vector3D.One, node.LocalTransform.Scale);
    }
}


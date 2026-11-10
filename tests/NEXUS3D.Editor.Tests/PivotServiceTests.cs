using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Engine.Services;
using Xunit;

namespace NEXUS3D.Editor.Tests;

public sealed class PivotServiceTests
{
    [Fact]
    public void SetPivot_PreserveVisualAppearance_MaintainsWorldPositions()
    {
        var service = new PivotService();
        var mesh = new MeshData("Mesh");
        var v0 = new Vector3D(0, 0, 0);
        var v1 = new Vector3D(10, 0, 0);
        var v2 = new Vector3D(0, 10, 0);
        mesh.Vertices.Add(new Vertex(v0));
        mesh.Vertices.Add(new Vertex(v1));
        mesh.Vertices.Add(new Vertex(v2));

        var node = new SceneNode("Node", new SceneObject("Obj") { Meshes = { mesh } })
        {
            LocalTransform = new Transform3D(new Vector3D(5, 5, 0), QuaternionD.Identity, new Vector3D(2, 2, 1))
        };

        // Record initial world positions before pivot shift
        var initialWorldV0 = node.GetWorldMatrix().TransformPoint(mesh.Vertices[0].Position);
        var initialWorldV1 = node.GetWorldMatrix().TransformPoint(mesh.Vertices[1].Position);
        var initialWorldV2 = node.GetWorldMatrix().TransformPoint(mesh.Vertices[2].Position);

        // Move pivot by (5, 5, 0) in local node space
        service.SetPivot(node, new Vector3D(5, 5, 0), preserveVisualAppearance: true);

        // Verify that rendered world coordinates after pivot modification are 100% IDENTICAL
        var newWorldV0 = node.GetWorldMatrix().TransformPoint(mesh.Vertices[0].Position);
        var newWorldV1 = node.GetWorldMatrix().TransformPoint(mesh.Vertices[1].Position);
        var newWorldV2 = node.GetWorldMatrix().TransformPoint(mesh.Vertices[2].Position);

        Assert.True((newWorldV0 - initialWorldV0).Length() < 1e-5, $"V0 jump detected: {newWorldV0} vs {initialWorldV0}");
        Assert.True((newWorldV1 - initialWorldV1).Length() < 1e-5, $"V1 jump detected: {newWorldV1} vs {initialWorldV1}");
        Assert.True((newWorldV2 - initialWorldV2).Length() < 1e-5, $"V2 jump detected: {newWorldV2} vs {initialWorldV2}");
    }

    [Fact]
    public void CenterPivot_PreservesGeometryAndSetsPivotToCenter()
    {
        var service = new PivotService();
        var mesh = new MeshData("Mesh");
        mesh.Vertices.Add(new Vertex(new Vector3D(-2, -2, -2)));
        mesh.Vertices.Add(new Vertex(new Vector3D(6, 6, 6))); // Center is (2, 2, 2)

        var node = new SceneNode("Node", new SceneObject("Obj") { Meshes = { mesh } })
        {
            LocalTransform = new Transform3D(new Vector3D(10, 0, 0), QuaternionD.Identity, Vector3D.One)
        };

        var initialWorld = node.GetWorldMatrix().TransformPoint(mesh.Vertices[0].Position);

        service.CenterPivot(node);

        var afterWorld = node.GetWorldMatrix().TransformPoint(mesh.Vertices[0].Position);
        Assert.True((afterWorld - initialWorld).Length() < 1e-5);
    }
}


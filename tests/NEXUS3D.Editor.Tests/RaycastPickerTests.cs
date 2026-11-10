using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Editor.Engine.Picking;
using Xunit;

namespace NEXUS3D.Editor.Tests;

public sealed class RaycastPickerTests
{
    [Fact]
    public void MöllerTrumbore_HitsInsideTriangle()
    {
        var v0 = new Vector3D(0, 0, 0);
        var v1 = new Vector3D(2, 0, 0);
        var v2 = new Vector3D(0, 2, 0);

        var ray = new Ray3D(new Vector3D(0.5, 0.5, 5), new Vector3D(0, 0, -1));

        bool hit = RaycastPicker.IntersectRayTriangle(ray, v0, v1, v2, out double dist, out Vector3D normal);

        Assert.True(hit);
        Assert.True(Math.Abs(dist - 5.0) < 1e-4);
        Assert.Equal(new Vector3D(0, 0, 1), normal);
    }

    [Fact]
    public void MöllerTrumbore_MissesOutsideTriangle()
    {
        var v0 = new Vector3D(0, 0, 0);
        var v1 = new Vector3D(2, 0, 0);
        var v2 = new Vector3D(0, 2, 0);

        var ray = new Ray3D(new Vector3D(2.5, 2.5, 5), new Vector3D(0, 0, -1));

        bool hit = RaycastPicker.IntersectRayTriangle(ray, v0, v1, v2, out _, out _);

        Assert.False(hit);
    }

    [Fact]
    public void Pick_SceneWithMesh_FindsClosestHit()
    {
        var picker = new RaycastPicker();
        var scene = new Scene3D { Name = "TestScene" };

        var mesh = new MeshData("Quad");
        mesh.Vertices.Add(new Vertex(new Vector3D(-1, -1, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(1, -1, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(1, 1, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(-1, 1, 0)));
        mesh.Faces.Add(new Face([0, 1, 2, 3])); // quad polygon

        var node = new SceneNode("TargetNode", new SceneObject("Obj") { Meshes = { mesh } });
        scene.Graph.AddNode(scene.Graph.Root, node);

        var ray = new Ray3D(new Vector3D(0, 0, 10), new Vector3D(0, 0, -1));
        var hit = picker.Pick(scene, ray);

        Assert.NotNull(hit);
        Assert.Equal(node.Id, hit.NodeId);
        Assert.Equal(mesh.Id, hit.MeshId);
        Assert.True(Math.Abs(hit.Distance - 10.0) < 1e-4);
        Assert.Equal(new Vector3D(0, 0, 0), hit.HitPoint);
    }

    [Fact]
    public void Pick_SceneWithVoxelGrid_FindsVoxelBoundsHit()
    {
        var picker = new RaycastPicker();
        var scene = new Scene3D { Name = "VoxelScene" };

        var grid = new VoxelGrid("TestGrid");
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 1));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 1, 1), 1));

        var node = new SceneNode("VoxelNode", new SceneObject("VoxelObj") { VoxelGrids = { grid } });
        scene.Graph.AddNode(scene.Graph.Root, node);

        var ray = new Ray3D(new Vector3D(0.5, 0.5, 10), new Vector3D(0, 0, -1));
        var hit = picker.Pick(scene, ray);

        Assert.NotNull(hit);
        Assert.Equal(node.Id, hit.NodeId);
        Assert.True(hit.Distance > 0);
    }
}

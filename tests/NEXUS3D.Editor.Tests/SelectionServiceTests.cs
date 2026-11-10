using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Engine.Services;
using Xunit;

namespace NEXUS3D.Editor.Tests;

public sealed class SelectionServiceTests
{
    [Fact]
    public void Select_SingleNode_SetsPrimaryAndSelection()
    {
        var service = new SelectionService();
        var id1 = Guid.NewGuid();

        service.Select(id1, multiSelect: false);

        Assert.Single(service.SelectedNodeIds);
        Assert.True(service.IsSelected(id1));
        Assert.Equal(id1, service.PrimarySelectedNodeId);
        Assert.Null(service.SelectedBoneId);
    }

    [Fact]
    public void Select_MultiSelect_AddsAndToggles()
    {
        var service = new SelectionService();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        service.Select(id1, multiSelect: false);
        service.Select(id2, multiSelect: true);

        Assert.Equal(2, service.SelectedNodeIds.Count);
        Assert.True(service.IsSelected(id1));
        Assert.True(service.IsSelected(id2));
        Assert.Equal(id2, service.PrimarySelectedNodeId);

        // Toggle id2 off
        service.Select(id2, multiSelect: true);
        Assert.Single(service.SelectedNodeIds);
        Assert.False(service.IsSelected(id2));
        Assert.Equal(id1, service.PrimarySelectedNodeId);
    }

    [Fact]
    public void SelectBone_ClearsNodeSelection()
    {
        var service = new SelectionService();
        var nodeId = Guid.NewGuid();
        var boneId = Guid.NewGuid();

        service.Select(nodeId);
        Assert.Single(service.SelectedNodeIds);

        service.SelectBone(boneId);
        Assert.Empty(service.SelectedNodeIds);
        Assert.Null(service.PrimarySelectedNodeId);
        Assert.Equal(boneId, service.SelectedBoneId);
    }

    [Fact]
    public void ClearSelection_ClearsAll()
    {
        var service = new SelectionService();
        service.Select(Guid.NewGuid());
        service.ClearSelection();

        Assert.Empty(service.SelectedNodeIds);
        Assert.Null(service.PrimarySelectedNodeId);
        Assert.Null(service.SelectedBoneId);
    }

    [Fact]
    public void GetSelectionWorldBounds_CalculatesCombinedBounds()
    {
        var service = new SelectionService();
        var scene = new Scene3D { Name = "TestScene" };

        var mesh1 = new MeshData("Mesh1");
        mesh1.Vertices.Add(new Vertex(new Vector3D(0, 0, 0)));
        mesh1.Vertices.Add(new Vertex(new Vector3D(2, 2, 2)));
        var node1 = new SceneNode("Node1", new SceneObject("Obj1") { Meshes = { mesh1 } });

        var mesh2 = new MeshData("Mesh2");
        mesh2.Vertices.Add(new Vertex(new Vector3D(5, 5, 5)));
        mesh2.Vertices.Add(new Vertex(new Vector3D(10, 10, 10)));
        var node2 = new SceneNode("Node2", new SceneObject("Obj2") { Meshes = { mesh2 } });

        scene.Graph.AddNode(scene.Graph.Root, node1);
        scene.Graph.AddNode(scene.Graph.Root, node2);

        service.Select(node1.Id, multiSelect: false);
        service.Select(node2.Id, multiSelect: true);

        var bounds = service.GetSelectionWorldBounds(scene);
        Assert.NotNull(bounds);
        Assert.Equal(new Vector3D(0, 0, 0), bounds.Value.Min);
        Assert.Equal(new Vector3D(10, 10, 10), bounds.Value.Max);

        var pivot = service.GetSelectionPivot(scene);
        Assert.NotNull(pivot);
        Assert.Equal(new Vector3D(5, 5, 5), pivot.Value);
    }
}

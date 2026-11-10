using System.Numerics;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class SceneRenderAdapterTests
{
    private readonly SceneRenderAdapter _adapter;

    public SceneRenderAdapterTests()
    {
        var boundsCalc = new BoundingBoxCalculator();
        var texLoader = new TextureLoader();
        var matAdapter = new MaterialRenderAdapter(texLoader);

        _adapter = new SceneRenderAdapter(matAdapter, boundsCalc);
    }

    [Fact]
    public void Adapt_EmptyScene_ReturnsValidEmptyRenderScene()
    {
        var scene = new Scene3D { Name = "Empty" };

        var renderScene = _adapter.Adapt(scene, RenderSettings.Default);

        Assert.NotNull(renderScene);
        Assert.Equal("Empty", renderScene.Name);
        Assert.Equal(0, renderScene.TotalTriangles);
        Assert.Equal(0, renderScene.TotalVertices);
        Assert.Equal(0, renderScene.TotalVoxels);
    }

    [Fact]
    public void Adapt_SceneWithMesh_GeneratesRenderMeshAndBounds()
    {
        var scene = new Scene3D { Name = "MeshScene" };
        var node = new SceneNode("Node1");
        var mesh = new MeshData
        {
            Name = "CubeMesh",
            Vertices =
            [
                new Vertex(new Vector3D(0, 0, 0)),
                new Vertex(new Vector3D(1, 0, 0)),
                new Vertex(new Vector3D(0, 1, 0))
            ],
            Faces = [new Face([0, 1, 2])]
        };
        var sceneObj = new SceneObject("MeshObj");
        sceneObj.Meshes.Add(mesh);
        node.Object = sceneObj;
        scene.Graph.Root.AddChild(node);

        var renderScene = _adapter.Adapt(scene, RenderSettings.Default);

        Assert.Equal(1, renderScene.TotalTriangles);
        Assert.Equal(3, renderScene.TotalVertices);
        Assert.NotEmpty(renderScene.RootNodes);
        Assert.NotEmpty(renderScene.RootNodes[0].Meshes);
    }

    [Fact]
    public void Adapt_SceneWithVoxels_GeneratesRenderVoxelChunks()
    {
        var scene = new Scene3D { Name = "VoxelScene" };
        var node = new SceneNode("VoxelNode");
        var grid = new VoxelGrid();
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 1, new BlockState("minecraft", "stone")));
        var sceneObj = new SceneObject("VoxelObj");
        sceneObj.VoxelGrids.Add(grid);
        node.Object = sceneObj;
        scene.Graph.Root.AddChild(node);

        var renderScene = _adapter.Adapt(scene, RenderSettings.Default);

        Assert.Equal(1, renderScene.TotalVoxels);
        Assert.NotEmpty(renderScene.RootNodes[0].VoxelChunks);
    }

    [Fact]
    public void Adapt_SceneWithSkeleton_ExtractsBones()
    {
        var scene = new Scene3D { Name = "SkelScene" };
        var skeleton = new SkeletonData("Biped");
        var rootBone = new BoneData("root");
        var childBone = new BoneData("head", rootBone.Id) { Pivot = new Vector3D(0, 10, 0) };
        skeleton.Bones.Add(rootBone);
        skeleton.Bones.Add(childBone);
        scene.Skeletons.Add(skeleton);

        var renderScene = _adapter.Adapt(scene, RenderSettings.Default);

        Assert.Single(renderScene.Skeletons);
        Assert.Equal("root", renderScene.Skeletons[0].Name);
        Assert.Single(renderScene.Skeletons[0].Children);
        Assert.Equal("head", renderScene.Skeletons[0].Children[0].Name);
    }
}

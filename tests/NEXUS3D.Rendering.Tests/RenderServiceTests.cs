using System.Numerics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class RenderServiceTests
{
    private readonly RenderService _service;

    public RenderServiceTests()
    {
        var boundsCalc = new BoundingBoxCalculator();
        var texLoader = new TextureLoader();
        var matAdapter = new MaterialRenderAdapter(texLoader);
        var adapter = new SceneRenderAdapter(matAdapter, boundsCalc);
        var camera = new CameraService();

        _service = new RenderService(adapter, camera, boundsCalc);
    }

    [Fact]
    public async Task LoadSceneAsync_SetsCurrentScene()
    {
        var scene = new Scene3D { Name = "TestScene" };

        var result = await _service.LoadSceneAsync(scene);

        Assert.NotNull(result);
        Assert.Same(result, _service.CurrentScene);
        Assert.Equal("TestScene", result.Name);
    }

    [Fact]
    public void UpdateSettings_ChangesCurrentSettings()
    {
        var newSettings = new RenderSettings { Shading = ShadingMode.Wireframe, ShowNormals = true };

        _service.UpdateSettings(newSettings);

        Assert.Equal(ShadingMode.Wireframe, _service.Settings.Shading);
        Assert.True(_service.Settings.ShowNormals);
    }

    [Fact]
    public async Task Selection_UpdatesStateProperly()
    {
        var scene = new Scene3D { Name = "SelectScene" };
        var node = new SceneNode("node-42");
        var obj = new SceneObject("Obj");
        obj.Meshes.Add(new MeshData
        {
            Vertices = [new Vertex(new Vector3D(0, 0, 0)), new Vertex(new Vector3D(1, 1, 1))],
            Faces = []
        });
        node.Object = obj;
        scene.Graph.Root.AddChild(node);

        var skeleton = new SkeletonData("Skel");
        var bone = new BoneData("arm_left") { Pivot = new Vector3D(1, 2, 3) };
        skeleton.Bones.Add(bone);
        scene.Skeletons.Add(skeleton);

        await _service.LoadSceneAsync(scene);

        var rNodeId = _service.CurrentScene!.RootNodes[0].Id;
        _service.SelectNode(rNodeId);
        Assert.Equal(rNodeId, _service.CurrentSelection.SelectedNodeId);
        Assert.Null(_service.CurrentSelection.SelectedBoneName);
        Assert.True(_service.CurrentSelection.HasSelection);

        _service.SelectBone("arm_left");
        Assert.Null(_service.CurrentSelection.SelectedNodeId);
        Assert.Equal("arm_left", _service.CurrentSelection.SelectedBoneName);
        Assert.True(_service.CurrentSelection.HasSelection);

        _service.ClearSelection();
        Assert.False(_service.CurrentSelection.HasSelection);
    }
}

using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.DependencyInjection;
using NEXUS3D.Rendering.Domain;
using Xunit;

namespace NEXUS3D.Rendering.Integration.Tests;

public sealed class RenderPipelineIntegrationTests
{
    private readonly ServiceProvider _services;

    public RenderPipelineIntegrationTests()
    {
        var sc = new ServiceCollection();
        sc.AddNexusRendering();
        _services = sc.BuildServiceProvider();
    }

    [Fact]
    public async Task FullRenderPipeline_DiResolutionAndSceneCycle_WorksEndToEnd()
    {
        var renderService = _services.GetRequiredService<IRenderService>();
        var cameraService = _services.GetRequiredService<ICameraService>();

        var scene = new Scene3D { Name = "IntegratedScene" };
        var node = new SceneNode("MainCharacter");
        var sceneObj = new SceneObject("CharacterPayload");

        var mesh = new MeshData
        {
            Name = "BodyMesh",
            Vertices =
            [
                new Vertex(new Vector3D(0, 0, 0)),
                new Vertex(new Vector3D(2, 0, 0)),
                new Vertex(new Vector3D(2, 3, 0)),
                new Vertex(new Vector3D(0, 3, 0))
            ],
            Faces = [new Face([0, 1, 2, 3])]
        };
        sceneObj.Meshes.Add(mesh);
        node.Object = sceneObj;
        scene.Graph.Root.AddChild(node);

        var skeleton = new SkeletonData("Biped");
        var hipBone = new BoneData("hip");
        var spineBone = new BoneData("spine", hipBone.Id) { Pivot = new Vector3D(0, 1.5, 0) };
        skeleton.Bones.Add(hipBone);
        skeleton.Bones.Add(spineBone);
        scene.Skeletons.Add(skeleton);

        // 1. Load scene
        var rScene = await renderService.LoadSceneAsync(scene);
        Assert.NotNull(rScene);
        Assert.Equal(2, rScene.TotalTriangles); // Quad = 2 tris
        Assert.Equal(4, rScene.TotalVertices);
        Assert.Single(rScene.Skeletons);

        // 2. Camera focus
        var cam = cameraService.FocusBounds(rScene.BoundsMin, rScene.BoundsMax);
        Assert.NotNull(cam);
        Assert.True(Vector3.Distance(cam.Position, cam.Target) > 0f);

        // 3. Preset switching
        var isoCam = cameraService.ApplyPreset(CameraPresetView.Isometric, rScene.BoundsCenter, 10f);
        Assert.Equal("Isometric", CameraPresetView.Isometric.ToString());
        Assert.Equal(rScene.BoundsCenter, isoCam.Target);

        // 4. Selection
        var nodeId = rScene.RootNodes[0].Id;
        renderService.SelectNode(nodeId);
        Assert.True(renderService.CurrentSelection.HasSelection);
        Assert.Equal(nodeId, renderService.CurrentSelection.SelectedNodeId);

        renderService.SelectBone("spine");
        Assert.True(renderService.CurrentSelection.HasSelection);
        Assert.Equal("spine", renderService.CurrentSelection.SelectedBoneName);

        renderService.ClearSelection();
        Assert.False(renderService.CurrentSelection.HasSelection);
    }
}

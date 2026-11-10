using System.Text;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Commands;
using NEXUS3D.Editor.Engine.Picking;
using NEXUS3D.Editor.Engine.Services;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Obj;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Formats.VoxelJson;
using Xunit;

namespace NEXUS3D.Editor.Integration.Tests;

public sealed class EditorIntegrationTests
{
    [Fact]
    public async Task ObjModel_LoadSelectTransformUndoRedo_EndToEnd()
    {
        string objContent = @"
v 0.0 0.0 0.0
v 1.0 0.0 0.0
v 0.0 1.0 0.0
f 1 2 3
";
        var parser = new ObjFormatParser();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(objContent));
        var parseResult = await parser.ParseToSceneAsync(stream, new ParseOptions());
        Assert.True(parseResult.IsSuccess);
        var scene = parseResult.Value!;

        var selectionService = new SelectionService();
        var transformService = new TransformService();
        var undoRedoService = new UndoRedoService();
        var picker = new RaycastPicker();

        // 1. Raycast picking from camera
        var pickRay = new Ray3D(new Vector3D(0.2, 0.2, 5.0), new Vector3D(0, 0, -1));
        var hit = picker.Pick(scene, pickRay);
        Assert.NotNull(hit);

        // 2. Select the hit node
        selectionService.Select(hit.NodeId);
        Assert.True(selectionService.IsSelected(hit.NodeId));

        var targetNode = scene.Graph.FindNode(hit.NodeId)!;
        var initialTransform = targetNode.LocalTransform.DeepClone();

        // 3. Execute Translate action via UndoRedo command
        var newTransform = initialTransform.DeepClone();
        newTransform.Position += new Vector3D(10, 5, 0);

        var cmd = new TransformCommand("Translate OBJ Model", targetNode, initialTransform, newTransform);
        undoRedoService.Execute(cmd);

        Assert.Equal(new Vector3D(10, 5, 0), targetNode.LocalTransform.Position);

        // 4. Undo
        undoRedoService.Undo();
        Assert.Equal(initialTransform.Position, targetNode.LocalTransform.Position);

        // 5. Redo
        undoRedoService.Redo();
        Assert.Equal(new Vector3D(10, 5, 0), targetNode.LocalTransform.Position);
    }

    [Fact]
    public void VoxelGrid_PivotEditAndBakeTransform_EndToEnd()
    {
        var scene = new Scene3D { Name = "VoxelScene" };
        var grid = new VoxelGrid("MyVoxelGrid");
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 1));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 1, 1), 1));

        var mesh = new MeshData("VoxelBlockMesh");
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(1, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 1, 0)));
        mesh.Faces.Add(new Face([0, 1, 2]));

        var node = new SceneNode("VoxelNode", new SceneObject("VoxelData")
        {
            VoxelGrids = { grid },
            Meshes = { mesh }
        })
        {
            LocalTransform = new Transform3D(new Vector3D(5, 5, 5), QuaternionD.Identity, new Vector3D(2, 2, 2))
        };
        scene.Graph.AddNode(scene.Graph.Root, node);

        var pivotService = new PivotService();
        var transformService = new TransformService();

        // 1. Center Pivot
        var initialWorldV0 = node.GetWorldMatrix().TransformPoint(mesh.Vertices[0].Position);
        pivotService.CenterPivot(node);
        var afterWorldV0 = node.GetWorldMatrix().TransformPoint(mesh.Vertices[0].Position);
        Assert.True((afterWorldV0 - initialWorldV0).Length() < 1e-5);

        // 2. Apply / Bake Transform
        transformService.ApplyTransform([node]);

        // Node local transform must now be identity
        Assert.Equal(Vector3D.Zero, node.LocalTransform.Position);
        Assert.Equal(Vector3D.One, node.LocalTransform.Scale);

        // Rendered world position must remain preserved
        var bakedWorldV0 = node.GetWorldMatrix().TransformPoint(mesh.Vertices[0].Position);
        Assert.True((bakedWorldV0 - initialWorldV0).Length() < 1e-5);
    }

    [Fact]
    public void BoneHierarchy_ReparentWithKinematics_EndToEnd()
    {
        var skeleton = new SkeletonData("Humanoid");
        var boneService = new BoneEditorService();

        var pelvis = boneService.AddBone(skeleton, "Pelvis", null, Vector3D.Zero);
        pelvis.LocalTransform = new Transform3D(new Vector3D(0, 10, 0), QuaternionD.Identity, Vector3D.One);

        var spine = boneService.AddBone(skeleton, "Spine", pelvis.Id, new Vector3D(0, 10, 0));
        spine.LocalTransform = new Transform3D(new Vector3D(0, 5, 0), QuaternionD.Identity, Vector3D.One);

        var arm = boneService.AddBone(skeleton, "Arm", null, new Vector3D(5, 15, 0));

        // Reparent arm under spine
        bool reparented = boneService.ReparentBone(skeleton, arm.Id, spine.Id);
        Assert.True(reparented);
        Assert.Equal(spine.Id, arm.ParentBoneId);

        // Kinematics world transforms
        var worldTransforms = boneService.CalculateWorldTransforms(skeleton);
        Assert.True(worldTransforms.ContainsKey(arm.Id));
    }
}

using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Factories;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Domain.Tests.Scene;

public sealed class SceneGraphAndCoordinateTests
{
    [Fact]
    public void SceneGraph_AddRemoveReparentAndCyclePrevention_ShouldEnforceInvariants()
    {
        var sceneFactory = new SceneFactory();
        var nodeFactory = new SceneNodeFactory();

        Scene3D scene = sceneFactory.Create("TestScene");
        SceneNode nodeA = nodeFactory.CreateEmptyNode("NodeA", new Transform3D(new Vector3D(10, 0, 0), QuaternionD.Identity, Vector3D.One));
        SceneNode nodeB = nodeFactory.CreateEmptyNode("NodeB", new Transform3D(new Vector3D(5, 2, 0), QuaternionD.Identity, Vector3D.One));
        SceneNode nodeC = nodeFactory.CreateEmptyNode("NodeC", new Transform3D(new Vector3D(0, 3, 1), QuaternionD.Identity, Vector3D.One));

        Assert.True(scene.Graph.AddNode(scene.Graph.Root, nodeA));
        Assert.True(scene.Graph.AddNode(nodeA, nodeB));
        Assert.True(scene.Graph.AddNode(nodeB, nodeC));

        // Cannot add an already-parented node again
        Assert.False(scene.Graph.AddNode(scene.Graph.Root, nodeB));

        // World matrix accumulation: Root -> NodeA (10,0,0) -> NodeB (5,2,0) -> NodeC (0,3,1) => (15, 5, 1)
        Vector3D worldTrans = nodeC.GetWorldMatrix().Translation;
        Assert.Equal(15.0, worldTrans.X, 6);
        Assert.Equal(5.0, worldTrans.Y, 6);
        Assert.Equal(1.0, worldTrans.Z, 6);

        // Prevent cycle: cannot reparent NodeA under its descendant NodeC
        Assert.False(scene.Graph.ReparentNode(nodeA.Id, nodeC.Id));
        Assert.False(scene.Graph.ContainsCycle());

        // Valid reparent with world position preservation
        Assert.True(scene.Graph.ReparentNode(nodeC.Id, scene.Graph.Root.Id, preserveWorldPosition: true));
        Assert.Equal(scene.Graph.Root.Id, nodeC.ParentId);
        Vector3D reparentedWorld = nodeC.GetWorldMatrix().Translation;
        Assert.Equal(15.0, reparentedWorld.X, 6);
        Assert.Equal(5.0, reparentedWorld.Y, 6);
        Assert.Equal(1.0, reparentedWorld.Z, 6);

        // Remove NodeA removes both NodeA and its child NodeB
        Assert.True(scene.Graph.RemoveNode(nodeA.Id));
        Assert.Null(scene.FindNode(nodeA.Id));
        Assert.Null(scene.FindNode(nodeB.Id));
        Assert.NotNull(scene.FindNode(nodeC.Id));
    }

    [Fact]
    public void Scene3D_DeepCloneAndSnapshot_ShouldBeIndependentAndPreserveIds()
    {
        var sceneFactory = new SceneFactory();
        var meshFactory = new MeshFactory();
        var nodeFactory = new SceneNodeFactory();

        Scene3D scene = sceneFactory.Create("OriginalScene");
        var cubeNode = nodeFactory.CreateMeshNode("Cube", meshFactory.CreateBox("CubeMesh", new Vector3D(2, 2, 2)));
        scene.Graph.AddNode(scene.Graph.Root, cubeNode);

        SceneSnapshot snapshot = scene.CreateSnapshot("BeforeMove");
        Scene3D clone = scene.DeepClone(preserveIds: false);

        Assert.NotEqual(scene.Id, clone.Id);
        Assert.Equal(scene.Id, snapshot.SceneId);

        // Mutate original scene
        cubeNode.LocalTransform.Position = new Vector3D(99, 99, 99);
        scene.Name = "MutatedScene";

        // Clone is unaffected
        SceneNode clonedCube = clone.FindNode(cubeNode.Id)!;
        Assert.Equal(Vector3D.Zero, clonedCube.LocalTransform.Position);
        Assert.Equal("OriginalScene", clone.Name);

        // Snapshot restore recovers original state and IDs
        Scene3D restored = snapshot.Restore();
        Assert.Equal(scene.Id, restored.Id);
        Assert.Equal("OriginalScene", restored.Name);
        Assert.Equal(Vector3D.Zero, restored.FindNode(cubeNode.Id)!.LocalTransform.Position);
    }

    [Fact]
    public void CoordinateConverter_HandednessAxisAndUnitConversions_ShouldBeAccurate()
    {
        var converter = new CoordinateConverter();

        CoordinateSystem usmRightYUp = CoordinateSystem.DefaultUsm;
        CoordinateSystem bedrockLeftYUp = CoordinateSystem.BedrockDefault;
        CoordinateSystem blenderRightZUp = CoordinateSystem.ZUpRightHanded;

        Assert.True(converter.RequiresWindingFlip(usmRightYUp, bedrockLeftYUp));
        Assert.False(converter.RequiresWindingFlip(usmRightYUp, blenderRightZUp));

        // Unit conversion test: Meter -> Centimeter scales by 100
        var cmSystem = new CoordinateSystem
        {
            Handedness = CoordinateHandedness.RightHanded,
            UpAxis = AxisDirection.PositiveY,
            ForwardAxis = AxisDirection.PositiveZ,
            Unit = MeasurementUnit.Centimeter
        };

        Vector3D inCm = converter.ConvertPosition(new Vector3D(1.5, 2.0, -3.0), usmRightYUp, cmSystem);
        Assert.Equal(150.0, inCm.X, 6);
        Assert.Equal(200.0, inCm.Y, 6);
        Assert.Equal(-300.0, inCm.Z, 6);

        // Direction conversion preserves unit length (ignores unit scale)
        Vector3D dirInCm = converter.ConvertDirection(new Vector3D(0.0, 1.0, 0.0), usmRightYUp, cmSystem);
        Assert.Equal(1.0, dirInCm.Length(), 6);

        // Non-orthogonal system throws DomainException with USM006
        var invalidSystem = new CoordinateSystem
        {
            UpAxis = AxisDirection.PositiveY,
            ForwardAxis = AxisDirection.PositiveY
        };
        var ex = Assert.Throws<DomainException>(() => converter.CreateConversionMatrix(invalidSystem, usmRightYUp));
        Assert.Equal(UsmErrorCodes.InvalidCoordinateSystem, ex.ErrorCode);
    }
}

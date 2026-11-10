using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Engine.Services;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using Xunit;

namespace NEXUS3D.Conversion.Tests;

public sealed class CoordinateConversionTests
{
    private readonly CoordinateConversionService _service = new(NullLogger<CoordinateConversionService>.Instance);

    [Fact]
    public void AdaptCoordinates_FlipsHandedness_AndInvertsWindingOrder()
    {
        var scene = new Scene3D { Name = "TestScene" };
        var mesh = new MeshData("TriangleMesh", MeshTopology.Triangles);
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(1, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 1, 0)));
        mesh.Faces.Add(new Face([0, 1, 2]));

        var sceneObj = new SceneObject("Obj");
        sceneObj.Meshes.Add(mesh);
        var node = new SceneNode("MeshNode", sceneObj);
        scene.Graph.AddNode(scene.Graph.Root, node);

        var sourceCoords = CoordinateSystem.DefaultUsm; // RH
        var targetCoords = CoordinateSystem.BedrockDefault; // LH

        _service.AdaptCoordinates(scene, sourceCoords, targetCoords);

        // Face winding order should be reversed [2, 1, 0]
        Assert.Equal([2, 1, 0], mesh.Faces[0].VertexIndices);
        Assert.Equal(targetCoords.Handedness, scene.Coordinates.CurrentSystem.Handedness);
    }

    [Fact]
    public void AdaptCoordinates_ScalesVertices_AndTransformsBones()
    {
        var scene = new Scene3D { Name = "SkeletonScene" };
        var skeleton = new SkeletonData("Rig");
        var bone = new BoneData("Hand")
        {
            Pivot = new Vector3D(10, 20, 30),
            BindRotationDegrees = new Vector3D(45, 0, 0)
        };
        skeleton.Bones.Add(bone);
        scene.Skeletons.Add(skeleton);

        var sourceCoords = CoordinateSystem.DefaultUsm;
        var targetCoords = CoordinateSystem.WavefrontObjDefault;

        _service.AdaptCoordinates(scene, sourceCoords, targetCoords, new CoordinateTransformOptions(Scale: 2.5));

        Assert.Equal(25.0, bone.Pivot.X, 3);
        Assert.Equal(50.0, bone.Pivot.Y, 3);
        Assert.Equal(75.0, bone.Pivot.Z, 3);
    }
}

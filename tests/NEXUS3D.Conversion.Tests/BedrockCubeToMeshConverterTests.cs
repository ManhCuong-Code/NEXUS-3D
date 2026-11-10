using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Engine.Services;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using Xunit;

namespace NEXUS3D.Conversion.Tests;

public sealed class BedrockCubeToMeshConverterTests
{
    private readonly BedrockCubeToMeshConverter _converter = new(NullLogger<BedrockCubeToMeshConverter>.Instance);

    [Fact]
    public void ConvertBedrockCubesToMesh_HierarchyNodes_BakesTransformsIntoWorldVertices()
    {
        var scene = new Scene3D { Name = "BedrockRobot" };

        // Parent node with translation
        var parentMesh = new MeshData("BodyMesh", MeshTopology.Polygons);
        parentMesh.Vertices.Add(new Vertex(new Vector3D(0, 0, 0)));
        parentMesh.Vertices.Add(new Vertex(new Vector3D(1, 0, 0)));
        parentMesh.Vertices.Add(new Vertex(new Vector3D(0, 1, 0)));
        parentMesh.Faces.Add(new Face([0, 1, 2]));

        var parentObj = new SceneObject("BodyObj");
        parentObj.Meshes.Add(parentMesh);
        var parentNode = new SceneNode("Body", parentObj)
        {
            LocalTransform = new Transform3D(new Vector3D(10, 0, 0), QuaternionD.Identity, Vector3D.One)
        };
        scene.Graph.AddNode(scene.Graph.Root, parentNode);

        // Child node offset by another (5, 0, 0)
        var childMesh = new MeshData("ArmMesh", MeshTopology.Polygons);
        childMesh.Vertices.Add(new Vertex(new Vector3D(0, 0, 0)));
        childMesh.Vertices.Add(new Vertex(new Vector3D(0, 2, 0)));
        childMesh.Vertices.Add(new Vertex(new Vector3D(1, 0, 0)));
        childMesh.Faces.Add(new Face([0, 1, 2]));

        var childObj = new SceneObject("ArmObj");
        childObj.Meshes.Add(childMesh);
        var childNode = new SceneNode("Arm", childObj)
        {
            LocalTransform = new Transform3D(new Vector3D(5, 0, 0), QuaternionD.Identity, Vector3D.One)
        };
        scene.Graph.AddNode(parentNode, childNode);

        var result = _converter.ConvertBedrockCubesToMesh(scene, new BedrockCubeToMeshOptions(BakeTransforms: true));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(6, result.Value.Vertices.Count);
        Assert.Equal(2, result.Value.Faces.Count);

        // Child node world pos should be 10 + 5 = 15 on X
        Assert.Contains(result.Value.Vertices, v => Math.Abs(v.Position.X - 15.0) < 1e-4);
        Assert.Contains(result.Value.Vertices, v => Math.Abs(v.Position.X - 10.0) < 1e-4);
    }
}

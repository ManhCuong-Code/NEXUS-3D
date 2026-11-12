using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using Xunit;

namespace NEXUS3D.BedrockGeometry.Tests;

public sealed class BedrockIndividualCubeAndHierarchyTests
{
    private static string GetFixturePath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "Json", fileName));

    [Fact]
    public void Convert_BedrockGeometryToScene_CreatesDistinctChildCubeNodesWithFullMetadata()
    {
        var parser = new BedrockGeometryParser();
        string path = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parseResult = parser.ParseFile(path);

        Assert.False(parseResult.HasErrors);

        var converter = new BedrockGeometryToSceneConverter();
        var (scene, report) = converter.Convert(parseResult.Document);

        Assert.NotNull(scene);
        Assert.True(report.CubeCount > 0);
        Assert.True(report.GeneratedMeshCount > 0);

        // Find the right_hand_weapon bone node
        var weaponNode = scene.Graph.Traverse().FirstOrDefault(n => n.Name == "right_hand_weapon");
        Assert.NotNull(weaponNode);
        Assert.NotNull(weaponNode.ParentNode);
        Assert.Equal("right_arm", weaponNode.ParentNode.Name);

        // Verify weaponNode has child cube node
        Assert.NotEmpty(weaponNode.Children);
        var cubeNode = weaponNode.Children.FirstOrDefault(c => c.Name.Contains("cube"));
        Assert.NotNull(cubeNode);
        Assert.Equal(weaponNode.Id, cubeNode.ParentId);
        Assert.Same(weaponNode, cubeNode.ParentNode);

        // Verify child cube has its own SceneObject and MeshData
        Assert.NotNull(cubeNode.Object);
        Assert.Single(cubeNode.Object.Meshes);
        var mesh = cubeNode.Object.Meshes[0];
        Assert.Equal(cubeNode.Name, mesh.Name);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Faces);

        // Verify full cube metadata properties
        var props = cubeNode.Object.Properties;
        Assert.True(props.ContainsKey("CubeId"), "CubeId property should exist");
        Assert.True(props.ContainsKey("Name"), "Name property should exist");
        Assert.True(props.ContainsKey("GeometryId"), "GeometryId property should exist");
        Assert.True(props.ContainsKey("BoneId"), "BoneId property should exist");
        Assert.True(props.ContainsKey("BoneName"), "BoneName property should exist");
        Assert.True(props.ContainsKey("ParentBone"), "ParentBone property should exist");
        Assert.True(props.ContainsKey("Origin"), "Origin property should exist");
        Assert.True(props.ContainsKey("Size"), "Size property should exist");
        Assert.True(props.ContainsKey("Pivot"), "Pivot property should exist");
        Assert.True(props.ContainsKey("Rotation"), "Rotation property should exist");
        Assert.True(props.ContainsKey("UV"), "UV property should exist");
        Assert.True(props.ContainsKey("Inflate"), "Inflate property should exist");
        Assert.True(props.ContainsKey("Mirror"), "Mirror property should exist");
        Assert.True(props.ContainsKey("Material"), "Material property should exist");

        Assert.Equal("right_hand_weapon", props["BoneName"].GetString());
        Assert.Equal("right_arm", props["ParentBone"].GetString());
    }

    [Fact]
    public void Convert_SceneToBedrockGeometry_PreservesAllBonesAndChildCubeNodesOnRoundtrip()
    {
        var parser = new BedrockGeometryParser();
        string path = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parseResult = parser.ParseFile(path);

        var toSceneConverter = new BedrockGeometryToSceneConverter();
        var (scene, toSceneReport) = toSceneConverter.Convert(parseResult.Document);

        var fromSceneConverter = new SceneToBedrockGeometryConverter();
        var (docBack, fromSceneReport) = fromSceneConverter.Convert(scene);

        Assert.NotNull(docBack);
        Assert.Single(docBack.Geometries);
        var geo = docBack.Geometries[0];

        Assert.Equal(parseResult.Document.Geometries[0].Bones.Count, geo.Bones.Count);
        int totalCubesBack = geo.Bones.Sum(b => b.Cubes.Count);
        Assert.Equal(toSceneReport.CubeCount, totalCubesBack);

        // Verify right_hand_weapon bone retains its cubes
        var weaponBone = geo.Bones.FirstOrDefault(b => b.Name == "right_hand_weapon");
        Assert.NotNull(weaponBone);
        Assert.NotEmpty(weaponBone.Cubes);
    }

    [Fact]
    public void Convert_SynthesizeFromUsmSceneWithoutSnapshot_PreservesBonesAndChildCubes()
    {
        var parser = new BedrockGeometryParser();
        string path = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parseResult = parser.ParseFile(path);

        var toSceneConverter = new BedrockGeometryToSceneConverter();
        var (scene, toSceneReport) = toSceneConverter.Convert(parseResult.Document);

        // Strip the snapshot extension to force synthesis mode
        scene.Metadata.Extensions.Remove(BedrockGeometryToSceneConverter.ExtensionNamespace);

        var fromSceneConverter = new SceneToBedrockGeometryConverter();
        var (docSynth, synthReport) = fromSceneConverter.Convert(scene);

        Assert.NotNull(docSynth);
        Assert.Single(docSynth.Geometries);
        var geo = docSynth.Geometries[0];

        Assert.Equal(parseResult.Document.Geometries[0].Bones.Count, geo.Bones.Count);
        int totalCubesSynth = geo.Bones.Sum(b => b.Cubes.Count);
        Assert.True(totalCubesSynth > 0, "Synthesized document must retain cubes from child cube nodes");
        Assert.Equal(toSceneReport.CubeCount, totalCubesSynth);
    }

    [Fact]
    public void Synchronize_CubeNodeLocalTransformEdit_UpdatesBedrockCubeOrigin()
    {
        var parser = new BedrockGeometryParser();
        string path = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parseResult = parser.ParseFile(path);

        var toSceneConverter = new BedrockGeometryToSceneConverter();
        var (scene, _) = toSceneConverter.Convert(parseResult.Document);

        // Move a child cube node by (5, 2, -3) in USM
        var weaponNode = scene.Graph.Traverse().FirstOrDefault(n => n.Name == "right_hand_weapon");
        Assert.NotNull(weaponNode);
        var cubeNode = weaponNode.Children.FirstOrDefault(c => c.Name.Contains("cube"));
        Assert.NotNull(cubeNode);

        cubeNode.LocalTransform = new Transform3D(new Vector3D(5, 2, -3), QuaternionD.Identity, Vector3D.One);

        var fromSceneConverter = new SceneToBedrockGeometryConverter();
        var (docBack, _) = fromSceneConverter.Convert(scene);

        var weaponBone = docBack.Geometries[0].Bones.FirstOrDefault(b => b.Name == "right_hand_weapon");
        Assert.NotNull(weaponBone);
        Assert.NotEmpty(weaponBone.Cubes);

        var cube = weaponBone.Cubes[0];
        // In Bedrock space: -X in USM is +X in Bedrock, so delta is (-5, 2, -3)
        // Original origin was (-7.0, 12.0, -8.0)
        // With delta: -7 + (-5) = -12 on X, 12 + 2 = 14 on Y, -8 + (-3) = -11 on Z
        Assert.Equal(-12.0, cube.Origin.X, precision: 2);
        Assert.Equal(14.0, cube.Origin.Y, precision: 2);
        Assert.Equal(-11.0, cube.Origin.Z, precision: 2);
    }
}

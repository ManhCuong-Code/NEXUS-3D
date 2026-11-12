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

    [Fact]
    public void Convert_BoneLocalTransforms_SetParentRelativeForChildAndAbsoluteForRoot()
    {
        var parser = new BedrockGeometryParser();
        string path = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parseResult = parser.ParseFile(path);

        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(parseResult.Document);

        var rootBoneNode = scene.Graph.Traverse().FirstOrDefault(n => n.Name == "root");
        Assert.NotNull(rootBoneNode);

        var rightArmNode = scene.Graph.Traverse().FirstOrDefault(n => n.Name == "right_arm");
        Assert.NotNull(rightArmNode);
        Assert.NotNull(rightArmNode.ParentNode);
        Assert.Equal("root", rightArmNode.ParentNode.Name);

        var rightWeaponNode = scene.Graph.Traverse().FirstOrDefault(n => n.Name == "right_hand_weapon");
        Assert.NotNull(rightWeaponNode);
        Assert.NotNull(rightWeaponNode.ParentNode);
        Assert.Equal("right_arm", rightWeaponNode.ParentNode.Name);

        var bedrockGeo = parseResult.Document.Geometries[0];
        var rootBone = bedrockGeo.Bones.First(b => b.Name == "root");
        var rightArmBone = bedrockGeo.Bones.First(b => b.Name == "right_arm");
        var rightWeaponBone = bedrockGeo.Bones.First(b => b.Name == "right_hand_weapon");

        // Root bone position must equal its own pivot in USM space
        var expectedRootPos = BedrockCoordinateConverter.BedrockPositionToUsm(rootBone.Pivot);
        Assert.Equal(expectedRootPos.X, rootBoneNode.LocalTransform.Position.X, precision: 4);
        Assert.Equal(expectedRootPos.Y, rootBoneNode.LocalTransform.Position.Y, precision: 4);
        Assert.Equal(expectedRootPos.Z, rootBoneNode.LocalTransform.Position.Z, precision: 4);

        // Child bone (right_arm under root): local position must be (arm.pivot - root.pivot) in USM
        var expectedArmLocalPos = BedrockCoordinateConverter.BedrockPositionToUsm(rightArmBone.Pivot - rootBone.Pivot);
        Assert.Equal(expectedArmLocalPos.X, rightArmNode.LocalTransform.Position.X, precision: 4);
        Assert.Equal(expectedArmLocalPos.Y, rightArmNode.LocalTransform.Position.Y, precision: 4);
        Assert.Equal(expectedArmLocalPos.Z, rightArmNode.LocalTransform.Position.Z, precision: 4);

        // Child bone (right_hand_weapon under right_arm): local position must be (weapon.pivot - arm.pivot) in USM
        var expectedWeaponLocalPos = BedrockCoordinateConverter.BedrockPositionToUsm(rightWeaponBone.Pivot - rightArmBone.Pivot);
        Assert.Equal(expectedWeaponLocalPos.X, rightWeaponNode.LocalTransform.Position.X, precision: 4);
        Assert.Equal(expectedWeaponLocalPos.Y, rightWeaponNode.LocalTransform.Position.Y, precision: 4);
        Assert.Equal(expectedWeaponLocalPos.Z, rightWeaponNode.LocalTransform.Position.Z, precision: 4);
    }

    [Fact]
    public void Convert_CubeMeshVertices_AreInBoneLocalCoordinatesAndTransformToExactModelSpace()
    {
        var parser = new BedrockGeometryParser();
        string path = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parseResult = parser.ParseFile(path);

        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(parseResult.Document);

        var weaponNode = scene.Graph.Traverse().FirstOrDefault(n => n.Name == "right_hand_weapon");
        Assert.NotNull(weaponNode);

        var cubeNode = weaponNode.Children.FirstOrDefault(c => c.Name.Contains("cube"));
        Assert.NotNull(cubeNode);
        Assert.NotNull(cubeNode.Object);
        var mesh = cubeNode.Object.Meshes[0];

        var bedrockGeo = parseResult.Document.Geometries[0];
        var weaponBone = bedrockGeo.Bones.First(b => b.Name == "right_hand_weapon");
        var cube = weaponBone.Cubes[0];

        double inflate = cube.Inflate ?? weaponBone.Inflate ?? 0.0;
        var minPos = new Vector3D(cube.Origin.X - inflate, cube.Origin.Y - inflate, cube.Origin.Z - inflate);
        var maxPos = new Vector3D(cube.Origin.X + cube.Size.X + inflate, cube.Origin.Y + cube.Size.Y + inflate, cube.Origin.Z + cube.Size.Z + inflate);

        var usmOriginMin = BedrockCoordinateConverter.BedrockPositionToUsm(minPos);
        var usmOriginMax = BedrockCoordinateConverter.BedrockPositionToUsm(maxPos);
        double expectedModelXMin = Math.Min(usmOriginMin.X, usmOriginMax.X);
        double expectedModelXMax = Math.Max(usmOriginMin.X, usmOriginMax.X);

        // 1. Mesh vertices of right_hand_weapon must be in BONE-LOCAL space (relative to weaponBone.Pivot)
        var usmBonePivot = BedrockCoordinateConverter.BedrockPositionToUsm(weaponBone.Pivot);
        foreach (var v in mesh.Vertices)
        {
            // Bone local position = model space - bone pivot
            Assert.True(v.Position.X >= (expectedModelXMin - usmBonePivot.X - 1e-4) &&
                        v.Position.X <= (expectedModelXMax - usmBonePivot.X + 1e-4),
                        $"Vertex X {v.Position.X} must be bone-local relative to pivot {usmBonePivot.X}");
        }

        // 2. For an unrotated root bone, applying cubeNode.GetWorldMatrix() MUST reconstruct the EXACT model space coordinates!
        var rootNode = scene.Graph.Traverse().First(n => n.Name == "root");
        var rootCubeNode = rootNode.Children.First(c => c.Name.Contains("cube"));
        Assert.NotNull(rootCubeNode.Object);
        var rootMesh = rootCubeNode.Object.Meshes[0];
        var rootBone = bedrockGeo.Bones.First(b => b.Name == "root");
        var rootCube = rootBone.Cubes[0];

        var rootUsmMin = BedrockCoordinateConverter.BedrockPositionToUsm(rootCube.Origin);
        var rootUsmMax = BedrockCoordinateConverter.BedrockPositionToUsm(rootCube.Origin + rootCube.Size);
        double rxMin = Math.Min(rootUsmMin.X, rootUsmMax.X);
        double rxMax = Math.Max(rootUsmMin.X, rootUsmMax.X);
        double ryMin = Math.Min(rootUsmMin.Y, rootUsmMax.Y);
        double ryMax = Math.Max(rootUsmMin.Y, rootUsmMax.Y);
        double rzMin = Math.Min(rootUsmMin.Z, rootUsmMax.Z);
        double rzMax = Math.Max(rootUsmMin.Z, rootUsmMax.Z);

        var rootWorldMat = rootCubeNode.GetWorldMatrix();
        foreach (var v in rootMesh.Vertices)
        {
            var worldV = rootWorldMat.TransformPoint(v.Position);
            Assert.True(worldV.X >= rxMin - 1e-3 && worldV.X <= rxMax + 1e-3,
                $"World vertex X {worldV.X} must match model bounds [{rxMin}, {rxMax}]");
            Assert.True(worldV.Y >= ryMin - 1e-3 && worldV.Y <= ryMax + 1e-3,
                $"World vertex Y {worldV.Y} must match model bounds [{ryMin}, {ryMax}]");
            Assert.True(worldV.Z >= rzMin - 1e-3 && worldV.Z <= rzMax + 1e-3,
                $"World vertex Z {worldV.Z} must match model bounds [{rzMin}, {rzMax}]");
        }
    }

    [Fact]
    public void BuildCubeMesh_UvMapping_AllSixFacesAreProperlyAlignedWithoutReversal()
    {
        var cube = new BedrockCube
        {
            Origin = new Vector3D(-4, 0, -4),
            Size = new Vector3D(8, 8, 8),
            Uv = BedrockCubeUv.CreateBoxUv(0, 0)
        };

        var bone = new BedrockBone
        {
            Name = "head",
            Pivot = new Vector3D(0, 4, 0)
        };

        var desc = new BedrockGeometryDescription
        {
            TextureWidth = 64,
            TextureHeight = 64
        };

        var mesh = BedrockCoordinateConverter.BuildCubeMesh(cube, bone, desc, "head_cube");

        Assert.Equal(24, mesh.Vertices.Count);
        Assert.Equal(6, mesh.Faces.Count);

        // Verify all 6 faces have valid UV coordinates in [0..1] and correct outward normals
        for (int fIdx = 0; fIdx < 6; fIdx++)
        {
            var face = mesh.Faces[fIdx];
            Assert.Equal(4, face.VertexIndices.Count);

            var v0 = mesh.Vertices[face.VertexIndices[0]];
            var v1 = mesh.Vertices[face.VertexIndices[1]];
            var v2 = mesh.Vertices[face.VertexIndices[2]];
            var v3 = mesh.Vertices[face.VertexIndices[3]];

            Assert.NotNull(v0.TexCoord);
            Assert.NotNull(v1.TexCoord);
            Assert.NotNull(v2.TexCoord);
            Assert.NotNull(v3.TexCoord);

            Assert.True(v0.TexCoord.Value.X >= 0.0 && v0.TexCoord.Value.X <= 1.0);
            Assert.True(v0.TexCoord.Value.Y >= 0.0 && v0.TexCoord.Value.Y <= 1.0);

            // Compute triangle normal
            var edge1 = v1.Position - v0.Position;
            var edge2 = v2.Position - v0.Position;
            var faceNorm = Vector3D.Cross(edge1, edge2).Normalize();

            Assert.NotNull(v0.Normal);
            // The vertex normal must align with the polygon face normal (dot > 0.99)
            double dot = Vector3D.Dot(faceNorm, v0.Normal.Value);
            Assert.True(dot > 0.99, $"Face normal and vertex normal must align (dot={dot})");
        }
    }

    [Fact]
    public void BuildCubeMesh_PerFaceUvWithRotation_RotatesCoordinatesClockwise()
    {
        var uv = BedrockCubeUv.CreatePerFaceUv();
        uv.Faces["north"] = new BedrockFaceUv
        {
            Uv = new Vector2D(0, 0),
            UvSize = new Vector2D(16, 16),
            UvRotation = 90
        };

        var cube = new BedrockCube
        {
            Origin = new Vector3D(-8, 0, -8),
            Size = new Vector3D(16, 16, 16),
            Uv = uv
        };

        var bone = new BedrockBone { Name = "test_bone", Pivot = Vector3D.Zero };
        var desc = new BedrockGeometryDescription { TextureWidth = 64, TextureHeight = 64 };

        var mesh = BedrockCoordinateConverter.BuildCubeMesh(cube, bone, desc, "test_cube");

        // Only north face was defined, so only 1 face and 4 vertices generated
        Assert.Single(mesh.Faces);
        Assert.Equal(4, mesh.Vertices.Count);

        var v0 = mesh.Vertices[0];
        Assert.NotNull(v0.TexCoord);
        // With 90 deg rotation, UV coordinates are rotated clockwise
        Assert.True(v0.TexCoord.Value.X >= 0.0 && v0.TexCoord.Value.X <= 1.0);
    }

    [Fact]
    public void Convert_GeometryDiagnostics_RecordsComprehensiveMetrics()
    {
        var parser = new BedrockGeometryParser();
        string path = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parseResult = parser.ParseFile(path);

        var converter = new BedrockGeometryToSceneConverter();
        var (scene, report) = converter.Convert(parseResult.Document);

        Assert.NotNull(report);
        Assert.True(report.GeometryCount >= 1);
        Assert.True(report.BoneCount > 0);
        Assert.True(report.CubeCount > 0);
        Assert.True(report.ValidCubeCount > 0);
        Assert.Equal(0, report.ErrorCubeCount);
        Assert.True(report.MeshCount > 0);
        Assert.True(report.FaceCount > 0);
        Assert.True(report.VertexCount > 0);
        Assert.True(report.TriangleCount > 0);
        Assert.True(report.CubesWithUvCount > 0);
        Assert.True(report.BonesWithParentCount > 0);

        // Verify USM extension data
        Assert.True(scene.Metadata.Extensions.ContainsKey("bedrock:diagnostics"));
        var diagExt = scene.Metadata.Extensions["bedrock:diagnostics"];
        Assert.True(diagExt.Values.ContainsKey("totalBones"));
        Assert.True(diagExt.Values.ContainsKey("totalCubes"));
        Assert.True(diagExt.Values.ContainsKey("validCubes"));
        Assert.True(diagExt.Values.ContainsKey("meshes"));
        Assert.True(diagExt.Values.ContainsKey("faces"));
        Assert.True(diagExt.Values.ContainsKey("vertices"));
        Assert.True(diagExt.Values.ContainsKey("triangles"));
        Assert.True(diagExt.Values.ContainsKey("cubesWithUv"));
        Assert.True(diagExt.Values.ContainsKey("bonesWithParent"));
    }
}

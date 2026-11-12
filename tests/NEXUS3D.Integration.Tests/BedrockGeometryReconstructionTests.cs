using System.IO;
using System.Numerics;
using System.Text.Json;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Integration.Tests;

/// <summary>
/// Integration tests verifying:
/// 1. Bone local transform hierarchy chaining and cube vertices mapping to exact model-space bounds without doubling.
/// 2. UV mapping for all 6 cube faces (North, South, East, West, Up, Down) matches Bedrock spec (Box UV and Per-Face UV).
/// 3. Companion skin texture discovery across directories and full geometry diagnostics statistics.
/// </summary>
public sealed class BedrockGeometryReconstructionTests : IDisposable
{
    private readonly string _testTempDir;

    public BedrockGeometryReconstructionTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), $"Nexus3D_Reconstruction_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch { }
    }

    private static string GetFixturePath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "Json", fileName));

    #region 1. Bone Local Transform Hierarchy Chaining & Model-Space Reconstruction

    [Fact]
    public void BoneHierarchy_LocalTransforms_AreRelative_ToParentBonePivot()
    {
        string fixturePath = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parser = new BedrockGeometryParser();
        var doc = parser.ParseFile(fixturePath).Document;

        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(doc);

        var rootNode = scene.Graph.Traverse().First(n => n.Name == "root");
        var rightArmNode = scene.Graph.Traverse().First(n => n.Name == "right_arm");
        var weaponNode = scene.Graph.Traverse().First(n => n.Name == "right_hand_weapon");

        // Hierarchy structure: geometry -> root -> right_arm -> right_hand_weapon
        Assert.NotNull(rootNode.ParentNode);
        Assert.Equal("geometry.nexus_guardian", rootNode.ParentNode.Name);
        Assert.Equal(rootNode.Id, rightArmNode.ParentId);
        Assert.Equal(rightArmNode.Id, weaponNode.ParentId);

        // Bedrock pivots:
        // root: [0, 0, 0] -> USM (0, 0, 0)
        // right_arm: [-5, 22, 0] -> USM (5, 22, 0)
        // right_hand_weapon: [-6, 14, 0] -> USM (6, 14, 0)
        //
        // Local transforms:
        // root (no parent): local position = (0, 0, 0)
        // right_arm (parent root): local position = (5 - 0, 22 - 0, 0 - 0) = (5, 22, 0)
        // right_hand_weapon (parent right_arm): local position = (6 - 5, 14 - 22, 0 - 0) = (1, -8, 0)

        Assert.Equal(0.0, rootNode.LocalTransform.Position.X, precision: 3);
        Assert.Equal(0.0, rootNode.LocalTransform.Position.Y, precision: 3);
        Assert.Equal(0.0, rootNode.LocalTransform.Position.Z, precision: 3);

        Assert.Equal(5.0, rightArmNode.LocalTransform.Position.X, precision: 3);
        Assert.Equal(22.0, rightArmNode.LocalTransform.Position.Y, precision: 3);
        Assert.Equal(0.0, rightArmNode.LocalTransform.Position.Z, precision: 3);

        Assert.Equal(1.0, weaponNode.LocalTransform.Position.X, precision: 3);
        Assert.Equal(-8.0, weaponNode.LocalTransform.Position.Y, precision: 3);
        Assert.Equal(0.0, weaponNode.LocalTransform.Position.Z, precision: 3);
    }

    [Fact]
    public void CubeVertices_AreBoneLocal_AndWorldTransformReconstructsExactModelSpaceBounds()
    {
        // Construct a synthetic Bedrock model with unrotated root and unrotated child bones
        // to strictly verify that local vertex coords + chained world transform = exact model bounds without doubling.
        var doc = new BedrockGeometryDocument
        {
            FormatVersion = "1.12.0"
        };

        var geo = new BedrockGeometry
        {
            Description = new BedrockGeometryDescription
            {
                Identifier = "geometry.test_chain",
                TextureWidth = 64,
                TextureHeight = 64
            }
        };

        // Parent bone at Bedrock pivot [-10, 20, 5] (USM: [10, 20, 5])
        var parentBone = new BedrockBone
        {
            Name = "parent_bone",
            Pivot = new Vector3D(-10, 20, 5),
            Rotation = Vector3D.Zero
        };
        // Cube on parent bone: origin [-12, 18, 3], size [4, 6, 4]
        // Bedrock bounds: X: [-12, -8], Y: [18, 24], Z: [3, 7]
        // USM model bounds: X: [8, 12], Y: [18, 24], Z: [3, 7]
        parentBone.Cubes.Add(new BedrockCube
        {
            Origin = new Vector3D(-12, 18, 3),
            Size = new Vector3D(4, 6, 4),
            Uv = BedrockCubeUv.CreateBoxUv(0, 0)
        });

        // Child bone at Bedrock pivot [-15, 30, 10] (USM: [15, 30, 10])
        var childBone = new BedrockBone
        {
            Name = "child_bone",
            Parent = "parent_bone",
            Pivot = new Vector3D(-15, 30, 10),
            Rotation = Vector3D.Zero
        };
        // Cube on child bone: origin [-18, 28, 8], size [6, 4, 4]
        // Bedrock bounds: X: [-18, -12], Y: [28, 32], Z: [8, 12]
        // USM model bounds: X: [12, 18], Y: [28, 32], Z: [8, 12]
        childBone.Cubes.Add(new BedrockCube
        {
            Origin = new Vector3D(-18, 28, 8),
            Size = new Vector3D(6, 4, 4),
            Uv = BedrockCubeUv.CreateBoxUv(0, 0)
        });

        geo.Bones.Add(parentBone);
        geo.Bones.Add(childBone);
        doc.Geometries.Add(geo);

        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(doc);

        var parentNode = scene.Graph.Traverse().First(n => n.Name == "parent_bone");
        var childNode = scene.Graph.Traverse().First(n => n.Name == "child_bone");

        // 1. Check parent bone local position = (10, 20, 5)
        Assert.Equal(new Vector3D(10, 20, 5), parentNode.LocalTransform.Position);

        // 2. Check child bone local position relative to parent = (15 - 10, 30 - 20, 10 - 5) = (5, 10, 5)
        Assert.Equal(new Vector3D(5, 10, 5), childNode.LocalTransform.Position);

        // 3. Child bone WorldMatrix translation should equal child bone pivot (15, 30, 10), NOT (25, 50, 15)!
        var childWorld = childNode.GetWorldMatrix();
        Assert.Equal(15.0, childWorld.Translation.X, precision: 3);
        Assert.Equal(30.0, childWorld.Translation.Y, precision: 3);
        Assert.Equal(10.0, childWorld.Translation.Z, precision: 3);

        // 4. Verify parent cube mesh vertices are bone-local: (corners - parentBone.Pivot)
        // Parent bone pivot is (10, 20, 5).
        // USM model bounds: [8, 12], [18, 24], [3, 7].
        // Bone-local bounds should be: [8 - 10, 12 - 10] = [-2, 2]; [18 - 20, 24 - 20] = [-2, 4]; [3 - 5, 7 - 5] = [-2, 2].
        var parentCubeNode = parentNode.Children.First(c => c.Name.Contains("cube"));
        var parentMesh = parentCubeNode.Object!.Meshes[0];
        var parentLocalBounds = parentMesh.Bounds;

        Assert.Equal(-2.0, parentLocalBounds.Min.X, precision: 3);
        Assert.Equal(2.0, parentLocalBounds.Max.X, precision: 3);
        Assert.Equal(-2.0, parentLocalBounds.Min.Y, precision: 3);
        Assert.Equal(4.0, parentLocalBounds.Max.Y, precision: 3);
        Assert.Equal(-2.0, parentLocalBounds.Min.Z, precision: 3);
        Assert.Equal(2.0, parentLocalBounds.Max.Z, precision: 3);

        // Applying parent bone WorldMatrix to mesh bounds reconstructs exact model bounds:
        var parentWorldMin = parentNode.GetWorldMatrix().TransformPoint(parentLocalBounds.Min);
        var parentWorldMax = parentNode.GetWorldMatrix().TransformPoint(parentLocalBounds.Max);
        Assert.Equal(8.0, Math.Min(parentWorldMin.X, parentWorldMax.X), precision: 3);
        Assert.Equal(12.0, Math.Max(parentWorldMin.X, parentWorldMax.X), precision: 3);
        Assert.Equal(18.0, Math.Min(parentWorldMin.Y, parentWorldMax.Y), precision: 3);
        Assert.Equal(24.0, Math.Max(parentWorldMin.Y, parentWorldMax.Y), precision: 3);

        // 5. Verify child cube mesh vertices are bone-local: (corners - childBone.Pivot)
        // Child bone pivot is (15, 30, 10).
        // USM model bounds: [12, 18], [28, 32], [8, 12].
        // Bone-local bounds should be: [12 - 15, 18 - 15] = [-3, 3]; [28 - 30, 32 - 30] = [-2, 2]; [8 - 10, 12 - 10] = [-2, 2].
        var childCubeNode = childNode.Children.First(c => c.Name.Contains("cube"));
        var childMesh = childCubeNode.Object!.Meshes[0];
        var childLocalBounds = childMesh.Bounds;

        Assert.Equal(-3.0, childLocalBounds.Min.X, precision: 3);
        Assert.Equal(3.0, childLocalBounds.Max.X, precision: 3);
        Assert.Equal(-2.0, childLocalBounds.Min.Y, precision: 3);
        Assert.Equal(2.0, childLocalBounds.Max.Y, precision: 3);
        Assert.Equal(-2.0, childLocalBounds.Min.Z, precision: 3);
        Assert.Equal(2.0, childLocalBounds.Max.Z, precision: 3);

        // Applying child bone WorldMatrix to mesh bounds reconstructs exact model bounds:
        var childWorldMin = childNode.GetWorldMatrix().TransformPoint(childLocalBounds.Min);
        var childWorldMax = childNode.GetWorldMatrix().TransformPoint(childLocalBounds.Max);
        Assert.Equal(12.0, Math.Min(childWorldMin.X, childWorldMax.X), precision: 3);
        Assert.Equal(18.0, Math.Max(childWorldMin.X, childWorldMax.X), precision: 3);
        Assert.Equal(28.0, Math.Min(childWorldMin.Y, childWorldMax.Y), precision: 3);
        Assert.Equal(32.0, Math.Max(childWorldMin.Y, childWorldMax.Y), precision: 3);
        Assert.Equal(8.0, Math.Min(childWorldMin.Z, childWorldMax.Z), precision: 3);
        Assert.Equal(12.0, Math.Max(childWorldMin.Z, childWorldMax.Z), precision: 3);
    }

    #endregion

    #region 2. Cube Face UV Mapping Across All 6 Faces

    [Fact]
    public void BuildCubeMesh_GeneratesAll6Faces_WithAccurateNormalsAndBedrockBoxUv()
    {
        var bone = new BedrockBone
        {
            Name = "test_bone",
            Pivot = Vector3D.Zero
        };

        var cube = new BedrockCube
        {
            Origin = new Vector3D(-4, 0, -2),
            Size = new Vector3D(8, 12, 4), // dx=8, dy=12, dz=4
            Uv = BedrockCubeUv.CreateBoxUv(16, 16)
        };

        var desc = new BedrockGeometryDescription
        {
            Identifier = "geometry.test",
            TextureWidth = 64,
            TextureHeight = 64
        };

        var mesh = BedrockCoordinateConverter.BuildCubeMesh(cube, bone, desc, "test_cube");

        // 6 faces * 4 vertices = 24 vertices; 6 quads
        Assert.Equal(24, mesh.Vertices.Count);
        Assert.Equal(6, mesh.Faces.Count);

        // Verify face normals
        // Face 0: north (-Z) -> normal (0, 0, -1)
        // Face 1: south (+Z) -> normal (0, 0, 1)
        // Face 2: east (+X) -> normal (1, 0, 0)
        // Face 3: west (-X) -> normal (-1, 0, 0)
        // Face 4: up (+Y) -> normal (0, 1, 0)
        // Face 5: down (-Y) -> normal (0, -1, 0)

        Assert.Equal(new Vector3D(0, 0, -1), mesh.Vertices[0].Normal);
        Assert.Equal(new Vector3D(0, 0, 1), mesh.Vertices[4].Normal);
        Assert.Equal(new Vector3D(1, 0, 0), mesh.Vertices[8].Normal);
        Assert.Equal(new Vector3D(-1, 0, 0), mesh.Vertices[12].Normal);
        Assert.Equal(new Vector3D(0, 1, 0), mesh.Vertices[16].Normal);
        Assert.Equal(new Vector3D(0, -1, 0), mesh.Vertices[20].Normal);

        // In Bedrock Box UV for offset [16, 16], dx=8, dy=12, dz=4:
        // Texture: 64x64
        // North face: u: [bx + dz, bx + dz + dx] = [16 + 4, 16 + 4 + 8] = [20, 28]
        //             v: [by + dz, by + dz + dy] = [16 + 4, 16 + 4 + 12] = [20, 32]
        // In USM normalized UV:
        // uNorm1 = 20 / 64 = 0.3125, uNorm2 = 28 / 64 = 0.4375
        // vNormTop = 1.0 - (20 / 64) = 0.6875
        // vNormBottom = 1.0 - (32 / 64) = 0.5000
        //
        // North vertices: [c1, c2, c3, c0] -> UVs: [BL, TL, TR, BR]
        var northBL = mesh.Vertices[0].TexCoord!.Value;
        var northTL = mesh.Vertices[1].TexCoord!.Value;
        var northTR = mesh.Vertices[2].TexCoord!.Value;
        var northBR = mesh.Vertices[3].TexCoord!.Value;

        Assert.Equal(0.3125, northBL.X, precision: 4);
        Assert.Equal(0.5000, northBL.Y, precision: 4);

        Assert.Equal(0.3125, northTL.X, precision: 4);
        Assert.Equal(0.6875, northTL.Y, precision: 4);

        Assert.Equal(0.4375, northTR.X, precision: 4);
        Assert.Equal(0.6875, northTR.Y, precision: 4);

        Assert.Equal(0.4375, northBR.X, precision: 4);
        Assert.Equal(0.5000, northBR.Y, precision: 4);
    }

    [Fact]
    public void BuildCubeMesh_PerFaceUv_RotatesUvCoordinatesCorrectly()
    {
        var bone = new BedrockBone { Name = "b", Pivot = Vector3D.Zero };
        var desc = new BedrockGeometryDescription { Identifier = "g", TextureWidth = 64, TextureHeight = 64 };

        var cube = new BedrockCube
        {
            Origin = new Vector3D(0, 0, 0),
            Size = new Vector3D(2, 2, 2),
            Uv = BedrockCubeUv.CreatePerFaceUv()
        };

        // Define north face with 90 degree UV rotation
        cube.Uv.Faces["north"] = new BedrockFaceUv
        {
            Uv = new Vector2D(0, 0),
            UvSize = new Vector2D(16, 16),
            UvRotation = 90
        };

        var mesh = BedrockCoordinateConverter.BuildCubeMesh(cube, bone, desc, "per_face_cube");

        // North face is the only face defined
        Assert.Single(mesh.Faces);
        Assert.Equal(4, mesh.Vertices.Count);

        // Before rotation: BL=(0, 0.75), BR=(0.25, 0.75), TR=(0.25, 1.0), TL=(0, 1.0)
        // Rotated 90 deg clockwise: (BL, BR, TR, TL) becomes (BR, TR, TL, BL)
        // v0 is uvs[0] (uvBL = original BR) -> (0.25, 0.75)
        // v1 is uvs[1] (uvTL = original BL) -> (0.00, 0.75)
        // v2 is uvs[2] (uvTR = original TL) -> (0.00, 1.00)
        // v3 is uvs[3] (uvBR = original TR) -> (0.25, 1.00)
        var v0 = mesh.Vertices[0].TexCoord!.Value;
        var v1 = mesh.Vertices[1].TexCoord!.Value;
        var v2 = mesh.Vertices[2].TexCoord!.Value;
        var v3 = mesh.Vertices[3].TexCoord!.Value;

        Assert.Equal(0.25, v0.X, precision: 4);
        Assert.Equal(0.75, v0.Y, precision: 4);
        Assert.Equal(0.00, v1.X, precision: 4);
        Assert.Equal(0.75, v1.Y, precision: 4);
        Assert.Equal(0.00, v2.X, precision: 4);
        Assert.Equal(1.00, v2.Y, precision: 4);
        Assert.Equal(0.25, v3.X, precision: 4);
        Assert.Equal(1.00, v3.Y, precision: 4);
    }

    #endregion

    #region 3. Companion Skin Discovery & Diagnostics Statistics

    [Fact]
    public void MaterialRenderAdapter_DiscoversCompanionSkin_AcrossNamingConventions()
    {
        var textureLoader = new TextureLoader();
        var adapter = new MaterialRenderAdapter(textureLoader);

        // 1. Model-named texture: <cleanMat>.png
        string subDir1 = Path.Combine(_testTempDir, "case1");
        Directory.CreateDirectory(subDir1);
        string modelPng = Path.Combine(subDir1, "guardian.png");
        File.WriteAllBytes(modelPng, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var mat1 = new MaterialData("geometry.guardian_material");
        var renderMat1 = adapter.AdaptMaterial(mat1, subDir1);
        Assert.Equal(modelPng, renderMat1.DiffuseTexturePath);

        // 2. steve.png in textures/entity directory
        string subDir2 = Path.Combine(_testTempDir, "case2");
        string entityDir = Path.Combine(subDir2, "textures", "entity");
        Directory.CreateDirectory(entityDir);
        string stevePng = Path.Combine(entityDir, "steve.png");
        File.WriteAllBytes(stevePng, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var mat2 = new MaterialData("custom_skin_material");
        var renderMat2 = adapter.AdaptMaterial(mat2, subDir2);
        Assert.Equal(stevePng, renderMat2.DiffuseTexturePath);

        // 3. Fallback: single unambiguous .png in folder
        string subDir3 = Path.Combine(_testTempDir, "case3");
        Directory.CreateDirectory(subDir3);
        string singlePng = Path.Combine(subDir3, "character_custom_texture.png");
        File.WriteAllBytes(singlePng, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var mat3 = new MaterialData("unrelated_material_name");
        var renderMat3 = adapter.AdaptMaterial(mat3, subDir3);
        Assert.Equal(singlePng, renderMat3.DiffuseTexturePath);
    }

    [Fact]
    public void BedrockGeometry_ConversionReport_PopulatesCompleteDiagnosticsStatistics()
    {
        string fixturePath = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parser = new BedrockGeometryParser();
        var doc = parser.ParseFile(fixturePath).Document;

        var converter = new BedrockGeometryToSceneConverter();
        var (scene, report) = converter.Convert(doc);

        Assert.NotNull(report);
        Assert.Equal(1, report.GeometryCount);
        Assert.True(report.BoneCount >= 3);
        Assert.True(report.CubeCount >= 3);
        Assert.Equal(report.CubeCount, report.ValidCubeCount);
        Assert.Equal(0, report.ErrorCubeCount);

        Assert.True(report.MeshCount > 0);
        Assert.True(report.FaceCount > 0);
        Assert.True(report.VertexCount > 0);
        Assert.True(report.TriangleCount > 0);
        Assert.True(report.CubesWithUvCount > 0);
        Assert.True(report.BonesWithParentCount > 0);

        // Verify diagnostics extension in USM scene metadata
        Assert.True(scene.Metadata.Extensions.ContainsKey("bedrock:diagnostics"));
        var diagExt = scene.Metadata.Extensions["bedrock:diagnostics"];
        Assert.Equal(report.BoneCount, diagExt.Values["totalBones"].GetInt32());
        Assert.Equal(report.CubeCount, diagExt.Values["totalCubes"].GetInt32());
        Assert.Equal(report.ValidCubeCount, diagExt.Values["validCubes"].GetInt32());
        Assert.Equal(report.FaceCount, diagExt.Values["faces"].GetInt32());
        Assert.Equal(report.VertexCount, diagExt.Values["vertices"].GetInt32());
    }

    #endregion
}

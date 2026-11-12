using System.IO;
using NEXUS3D.App.ViewModels;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using Xunit;

namespace NEXUS3D.Integration.Tests;

public sealed class BedrockWorkspaceViewModelTests
{
    private static string GetFixturePath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "Json", fileName));

    [Fact]
    public void LoadFromScene_BuildsGeometryBoneCubesTreeStructure()
    {
        var parser = new BedrockGeometryParser();
        var parseResult = parser.ParseFile(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var converter = new BedrockGeometryToSceneConverter();
        var (scene, report) = converter.Convert(parseResult.Document);

        var vm = new BedrockWorkspaceViewModel();
        vm.LoadFromScene(scene);

        Assert.Equal(report.BoneCount, vm.BoneCount);
        Assert.Equal(report.CubeCount, vm.CubeCount);

        // Verify Geometries collection (Geometry -> Bone -> Cubes)
        Assert.Single(vm.Geometries);
        var geo = vm.Geometries[0];
        Assert.Equal("geometry.nexus_guardian", geo.Identifier);
        Assert.NotEmpty(geo.Bones);

        // Root bone should have cubes and children
        var rootBone = geo.Bones.FirstOrDefault(b => b.Name == "root");
        Assert.NotNull(rootBone);
        Assert.NotEmpty(rootBone.Cubes);
        Assert.NotEmpty(rootBone.Children);

        // Child bone right_arm -> right_hand_weapon
        var armBone = rootBone.Children.FirstOrDefault(b => b.Name == "right_arm");
        Assert.NotNull(armBone);
        Assert.NotEmpty(armBone.Cubes);

        var weaponBone = armBone.Children.FirstOrDefault(b => b.Name == "right_hand_weapon");
        Assert.NotNull(weaponBone);
        Assert.Single(weaponBone.Cubes);

        var cube = weaponBone.Cubes[0];
        Assert.NotNull(cube.NodeId);
        Assert.Equal("right_hand_weapon_cube_0", cube.Name);
        Assert.Equal("right_hand_weapon", cube.BoneName);
        Assert.Equal("right_arm", cube.ParentBone);
        Assert.False(string.IsNullOrEmpty(cube.Origin));
        Assert.False(string.IsNullOrEmpty(cube.Size));
    }

    [Fact]
    public void LoadFromScene_ExposesAll14GeometryDiagnosticsAndMissingTextureWarning()
    {
        var parser = new BedrockGeometryParser();
        var parseResult = parser.ParseFile(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var converter = new BedrockGeometryToSceneConverter();
        var (scene, report) = converter.Convert(parseResult.Document);

        var vm = new BedrockWorkspaceViewModel();
        vm.LoadFromScene(scene);

        // 1. TotalGeometries
        Assert.Equal(1, vm.TotalGeometries);
        // 2. TotalBones
        Assert.Equal(report.BoneCount, vm.TotalBones);
        // 3. TotalCubes
        Assert.Equal(report.CubeCount, vm.TotalCubes);
        // 4. ValidCubes
        Assert.Equal(report.ValidCubeCount, vm.ValidCubes);
        // 5. ErrorCubes
        Assert.Equal(report.ErrorCubeCount, vm.ErrorCubes);
        // 6. Meshes
        Assert.Equal(report.MeshCount, vm.Meshes);
        // 7. Faces
        Assert.Equal(report.FaceCount, vm.Faces);
        // 8. Vertices
        Assert.Equal(report.VertexCount, vm.Vertices);
        // 9. Triangles
        Assert.Equal(report.TriangleCount, vm.Triangles);
        // 10. CubesWithUv
        Assert.Equal(report.CubesWithUvCount, vm.CubesWithUv);
        // 11. CubesMissingUv
        Assert.Equal(report.CubeCount - report.CubesWithUvCount, vm.CubesMissingUv);
        // 12. BonesWithParent
        Assert.Equal(report.BonesWithParentCount, vm.BonesWithParent);
        // 13. TexturesFound (no texture on disk in fixture dir)
        Assert.Equal(0, vm.TexturesFound);
        // 14. MissingTextures
        Assert.Equal(1, vm.MissingTextures);

        // Warning banner
        Assert.True(vm.HasMissingTextureWarning);
        Assert.Equal("Missing Texture: Không tìm thấy ảnh Skin tương ứng với Geometry.", vm.MissingTextureWarningMessage);
    }

    [Fact]
    public void LoadFromScene_WhenCompanionSkinFoundOnDisk_ClearsMissingTextureWarning()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "nexus3d_skin_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string geoFile = Path.Combine(tempDir, "model.geo.json");
            File.Copy(GetFixturePath("bedrock_1_16_0_guardian.geo.json"), geoFile);

            // Create companion skin file
            string skinFile = Path.Combine(tempDir, "nexus_guardian.png");
            File.WriteAllBytes(skinFile, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            var parser = new BedrockGeometryParser();
            var parseResult = parser.ParseFile(geoFile);
            var converter = new BedrockGeometryToSceneConverter();
            var (scene, _) = converter.Convert(parseResult.Document);

            var vm = new BedrockWorkspaceViewModel();
            vm.LoadFromScene(scene, geoFile);

            Assert.Equal(1, vm.TexturesFound);
            Assert.Equal(0, vm.MissingTextures);
            Assert.False(vm.HasMissingTextureWarning);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void SelectCube_UpdatesSelectionAndRaisesEventsToNotifyViewport()
    {
        var parser = new BedrockGeometryParser();
        var parseResult = parser.ParseFile(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(parseResult.Document);

        var vm = new BedrockWorkspaceViewModel();
        vm.LoadFromScene(scene);

        string? highlightedNodeId = null;
        string? selectedNodeId = null;
        vm.CubeHighlightRequested += (s, id) => highlightedNodeId = id;
        vm.CubeSelected += (s, id) => selectedNodeId = id;

        var weaponBone = vm.Geometries[0].Bones
            .SelectMany(b => GetAllBonesRecursive(b))
            .First(b => b.Name == "right_hand_weapon");

        var targetCube = weaponBone.Cubes[0];
        vm.SelectCube(targetCube);

        Assert.Same(targetCube, vm.SelectedCube);
        Assert.True(targetCube.IsSelected);
        Assert.Equal(targetCube.NodeId?.ToString(), highlightedNodeId);
        Assert.Equal(targetCube.NodeId?.ToString(), selectedNodeId);
    }

    [Fact]
    public void SelectNodeById_FromViewportSelection_HighlightsCubeInHierarchy()
    {
        var parser = new BedrockGeometryParser();
        var parseResult = parser.ParseFile(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(parseResult.Document);

        var vm = new BedrockWorkspaceViewModel();
        vm.LoadFromScene(scene);

        var weaponNode = scene.Graph.Traverse().First(n => n.Name == "right_hand_weapon");
        var cubeNode = weaponNode.Children.First(c => c.Name.Contains("cube"));

        // Simulate Viewport click selecting this cube by Node ID
        vm.SelectNodeById(cubeNode.Id.ToString());

        Assert.NotNull(vm.SelectedBone);
        Assert.Equal("right_hand_weapon", vm.SelectedBone.Name);
        Assert.True(vm.SelectedBone.IsSelected);
        Assert.True(vm.SelectedBone.IsExpanded);

        Assert.NotNull(vm.SelectedCube);
        Assert.Equal(cubeNode.Id, vm.SelectedCube.NodeId);
        Assert.True(vm.SelectedCube.IsSelected);
        Assert.Equal("right_hand_weapon", vm.SelectedCube.BoneName);
    }

    [Fact]
    public void CubePropertyEditing_RecalculatesMeshVerticesAndProperties()
    {
        var parser = new BedrockGeometryParser();
        var parseResult = parser.ParseFile(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(parseResult.Document);

        var vm = new BedrockWorkspaceViewModel();
        vm.LoadFromScene(scene);

        var weaponBone = vm.Geometries[0].Bones
            .SelectMany(b => GetAllBonesRecursive(b))
            .First(b => b.Name == "right_hand_weapon");

        var cube = weaponBone.Cubes[0];
        var cubeNode = scene.Graph.FindNode(cube.NodeId!.Value);
        Assert.NotNull(cubeNode);
        Assert.NotNull(cubeNode.Object);
        Assert.NotEmpty(cubeNode.Object.Meshes);

        var initialMesh = cubeNode.Object.Meshes[0];
        Assert.Equal(24, initialMesh.Vertices.Count);
        Vector3D initialV0 = initialMesh.Vertices[0].Position;

        bool meshUpdatedFired = false;
        bool viewportRefresherFired = false;
        vm.CubeMeshUpdated += (s, c) => meshUpdatedFired = true;
        vm.ViewportRefresher = () => viewportRefresherFired = true;

        // Interactive property edits
        cube.Origin = "20.0, 30.0, 40.0";
        cube.Size = "5.0, 6.0, 7.0";
        cube.Inflate = 0.5f;

        Assert.True(meshUpdatedFired);
        Assert.True(viewportRefresherFired);

        // Verify that vertices were recalculated by BedrockCoordinateConverter
        Vector3D updatedV0 = cubeNode.Object.Meshes[0].Vertices[0].Position;
        Assert.NotEqual(initialV0, updatedV0);

        // Verify updated SceneNode properties
        Assert.True(cubeNode.Object.Properties.ContainsKey("Origin"));
        Assert.True(cubeNode.Object.Properties.ContainsKey("Size"));
        Assert.True(cubeNode.Object.Properties.ContainsKey("Inflate"));
    }

    [Fact]
    public void ResetTransformCommand_RestoresOriginalBindPoseValuesAndVertices()
    {
        var parser = new BedrockGeometryParser();
        var parseResult = parser.ParseFile(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(parseResult.Document);

        var vm = new BedrockWorkspaceViewModel();
        vm.LoadFromScene(scene);

        var weaponBone = vm.Geometries[0].Bones
            .SelectMany(b => GetAllBonesRecursive(b))
            .First(b => b.Name == "right_hand_weapon");

        var cube = weaponBone.Cubes[0];
        var cubeNode = scene.Graph.FindNode(cube.NodeId!.Value);
        Assert.NotNull(cubeNode);
        Assert.NotNull(cubeNode.Object);

        string originalOrigin = cube.Origin;
        string originalSize = cube.Size;
        Vector3D originalV0 = cubeNode.Object.Meshes[0].Vertices[0].Position;

        // Perform edits
        cube.Origin = "99.0, 88.0, 77.0";
        cube.Size = "15.0, 16.0, 17.0";
        Assert.NotEqual(originalOrigin, cube.Origin);
        Assert.NotEqual(originalV0, cubeNode.Object.Meshes[0].Vertices[0].Position);

        // Select cube and execute ResetTransformCommand on BedrockWorkspaceViewModel
        vm.SelectCube(cube);
        vm.ResetTransformCommand.Execute(null);

        // Values must be restored to original bind pose
        Assert.Equal(originalOrigin, cube.Origin);
        Assert.Equal(originalSize, cube.Size);
        Assert.Equal(originalV0, cubeNode.Object.Meshes[0].Vertices[0].Position);
    }

    [Fact]
    public void ResetTransformCommand_OnCubeItemViewModelDirectly_RestoresBindPose()
    {
        var parser = new BedrockGeometryParser();
        var parseResult = parser.ParseFile(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(parseResult.Document);

        var vm = new BedrockWorkspaceViewModel();
        vm.LoadFromScene(scene);

        var weaponBone = vm.Geometries[0].Bones
            .SelectMany(b => GetAllBonesRecursive(b))
            .First(b => b.Name == "right_hand_weapon");

        var cube = weaponBone.Cubes[0];
        var cubeNode = scene.Graph.FindNode(cube.NodeId!.Value);
        Assert.NotNull(cubeNode);
        Assert.NotNull(cubeNode.Object);

        string originalOrigin = cube.Origin;
        string originalPivot = cube.Pivot;
        Vector3D originalV0 = cubeNode.Object.Meshes[0].Vertices[0].Position;

        // Perform edit
        cube.Origin = "123.0, 456.0, 789.0";
        cube.Pivot = "10.0, 20.0, 30.0";
        Assert.NotEqual(originalOrigin, cube.Origin);
        Assert.NotEqual(originalV0, cubeNode.Object.Meshes[0].Vertices[0].Position);

        // Execute ResetTransformCommand directly on BedrockCubeItemViewModel
        cube.ResetTransformCommand.Execute(null);

        Assert.Equal(originalOrigin, cube.Origin);
        Assert.Equal(originalPivot, cube.Pivot);
        Assert.Equal(originalV0, cubeNode.Object.Meshes[0].Vertices[0].Position);
    }

    [Fact]
    public void LoadFromScene_WithoutMetadataDiagnostics_CalculatesAll14FieldsDynamically()
    {
        var parser = new BedrockGeometryParser();
        var parseResult = parser.ParseFile(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var converter = new BedrockGeometryToSceneConverter();
        var (scene, report) = converter.Convert(parseResult.Document);

        // Remove the metadata extension to test dynamic fallback calculation
        scene.Metadata.Extensions.Remove("bedrock:diagnostics");

        var vm = new BedrockWorkspaceViewModel();
        vm.LoadFromScene(scene);

        Assert.Equal(1, vm.TotalGeometries);
        Assert.Equal(report.BoneCount, vm.TotalBones);
        Assert.Equal(report.CubeCount, vm.TotalCubes);
        Assert.Equal(report.ValidCubeCount, vm.ValidCubes);
        Assert.Equal(report.ErrorCubeCount, vm.ErrorCubes);
        Assert.Equal(report.MeshCount, vm.Meshes);
        Assert.Equal(report.FaceCount, vm.Faces);
        Assert.Equal(report.VertexCount, vm.Vertices);
        Assert.Equal(report.TriangleCount, vm.Triangles);
        Assert.Equal(report.CubesWithUvCount, vm.CubesWithUv);
        Assert.Equal(report.BonesWithParentCount, vm.BonesWithParent);
        Assert.True(vm.HasMissingTextureWarning);
    }

    private static IEnumerable<BedrockBoneNodeViewModel> GetAllBonesRecursive(BedrockBoneNodeViewModel bone)
    {
        yield return bone;
        foreach (var child in bone.Children)
        {
            foreach (var b in GetAllBonesRecursive(child))
            {
                yield return b;
            }
        }
    }
}

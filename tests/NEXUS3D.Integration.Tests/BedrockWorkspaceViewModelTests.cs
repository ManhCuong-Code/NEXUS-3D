using System.IO;
using NEXUS3D.App.ViewModels;
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

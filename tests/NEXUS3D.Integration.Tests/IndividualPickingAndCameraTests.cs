using System.IO;
using System.Numerics;
using System.Text.Json;
using NEXUS3D.App.ViewModels;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Picking;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Integration.Tests;

/// <summary>
/// Comprehensive integration tests verifying:
/// 1. Bedrock Geometry creates individual child SceneNodes for each cube with complete metadata.
/// 2. RaycastPicker calculates exact voxel coordinate (X, Y, Z) and block state when casting rays at a VoxelGrid.
/// 3. Bedrock export preserves all cubes and bones with individual cube child nodes (roundtrip &amp; synthesis).
/// 4. Camera framing &amp; workspace selection synchronization for cubes and blocks.
/// </summary>
public sealed class IndividualPickingAndCameraTests
{
    private static string GetFixturePath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "Json", fileName));

    #region 1. Bedrock Geometry Individual Child SceneNodes and Metadata

    [Fact]
    public void BedrockGeometry_CreatesIndividualChildSceneNodes_ForEachCube_WithCompleteMetadata()
    {
        string fixturePath = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        Assert.True(File.Exists(fixturePath), $"Fixture file must exist at {fixturePath}");

        var parser = new BedrockGeometryParser();
        var parseResult = parser.ParseFile(fixturePath);
        Assert.False(parseResult.HasErrors, "Guardian geometry fixture must parse without errors");

        var doc = parseResult.Document;
        Assert.Single(doc.Geometries);
        var originalGeo = doc.Geometries[0];

        var converter = new BedrockGeometryToSceneConverter();
        var (scene, report) = converter.Convert(doc);

        Assert.NotNull(scene);
        Assert.True(report.BoneCount > 0);
        Assert.True(report.CubeCount > 0);

        int expectedTotalCubes = originalGeo.Bones.Sum(b => b.Cubes.Count);
        Assert.Equal(expectedTotalCubes, report.CubeCount);

        var allNodes = scene.Graph.Traverse().ToList();
        var boneNames = originalGeo.Bones.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);

        // Verify that every bone containing cubes has distinct child SceneNodes for each cube
        foreach (var bedrockBone in originalGeo.Bones.Where(b => b.Cubes.Count > 0))
        {
            var boneNode = allNodes.FirstOrDefault(n => n.Name == bedrockBone.Name);
            Assert.NotNull(boneNode);

            // Filter child cube nodes (excluding child bones)
            var childCubeNodes = boneNode.Children
                .Where(c => !boneNames.Contains(c.Name))
                .ToList();

            Assert.Equal(bedrockBone.Cubes.Count, childCubeNodes.Count);

            for (int i = 0; i < bedrockBone.Cubes.Count; i++)
            {
                var cube = bedrockBone.Cubes[i];
                var cubeNode = childCubeNodes[i];

                // Hierarchy validation
                Assert.Equal(boneNode.Id, cubeNode.ParentId);
                Assert.Same(boneNode, cubeNode.ParentNode);

                // Object & Mesh validation
                Assert.NotNull(cubeNode.Object);
                Assert.Single(cubeNode.Object.Meshes);
                var mesh = cubeNode.Object.Meshes[0];
                Assert.Equal(cubeNode.Name, mesh.Name);
                Assert.NotEmpty(mesh.Vertices);
                Assert.NotEmpty(mesh.Faces);

                // Complete metadata verification on SceneObject.Properties
                var props = cubeNode.Object.Properties;
                Assert.True(props.ContainsKey("CubeId"), $"Cube node {cubeNode.Name} must contain 'CubeId'");
                Assert.True(props.ContainsKey("Name"), $"Cube node {cubeNode.Name} must contain 'Name'");
                Assert.True(props.ContainsKey("GeometryId"), $"Cube node {cubeNode.Name} must contain 'GeometryId'");
                Assert.True(props.ContainsKey("BoneId"), $"Cube node {cubeNode.Name} must contain 'BoneId'");
                Assert.True(props.ContainsKey("BoneName"), $"Cube node {cubeNode.Name} must contain 'BoneName'");
                Assert.True(props.ContainsKey("ParentBone"), $"Cube node {cubeNode.Name} must contain 'ParentBone'");
                Assert.True(props.ContainsKey("Origin"), $"Cube node {cubeNode.Name} must contain 'Origin'");
                Assert.True(props.ContainsKey("Size"), $"Cube node {cubeNode.Name} must contain 'Size'");
                Assert.True(props.ContainsKey("Pivot"), $"Cube node {cubeNode.Name} must contain 'Pivot'");
                Assert.True(props.ContainsKey("Rotation"), $"Cube node {cubeNode.Name} must contain 'Rotation'");
                Assert.True(props.ContainsKey("UV"), $"Cube node {cubeNode.Name} must contain 'UV'");
                Assert.True(props.ContainsKey("Inflate"), $"Cube node {cubeNode.Name} must contain 'Inflate'");
                Assert.True(props.ContainsKey("Mirror"), $"Cube node {cubeNode.Name} must contain 'Mirror'");
                Assert.True(props.ContainsKey("Material"), $"Cube node {cubeNode.Name} must contain 'Material'");

                // Also verify bedrock.* prefixed properties
                Assert.True(props.ContainsKey("bedrock.cubeId"));
                Assert.True(props.ContainsKey("bedrock.cubeIndex"));
                Assert.True(props.ContainsKey("bedrock.origin"));
                Assert.True(props.ContainsKey("bedrock.size"));

                // Metadata content validation
                Assert.Equal(cubeNode.Name, props["Name"].GetString());
                Assert.Equal(bedrockBone.Name, props["BoneName"].GetString());
                Assert.Equal(bedrockBone.Parent ?? string.Empty, props["ParentBone"].GetString());

                var originArray = props["Origin"].EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Assert.Equal(3, originArray.Length);
                Assert.Equal(cube.Origin.X, originArray[0], precision: 4);
                Assert.Equal(cube.Origin.Y, originArray[1], precision: 4);
                Assert.Equal(cube.Origin.Z, originArray[2], precision: 4);

                var sizeArray = props["Size"].EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Assert.Equal(3, sizeArray.Length);
                Assert.Equal(cube.Size.X, sizeArray[0], precision: 4);
                Assert.Equal(cube.Size.Y, sizeArray[1], precision: 4);
                Assert.Equal(cube.Size.Z, sizeArray[2], precision: 4);
            }
        }

        // Specific check on right_hand_weapon bone
        var weaponNode = allNodes.First(n => n.Name == "right_hand_weapon");
        Assert.Single(weaponNode.Children);
        var weaponCubeNode = weaponNode.Children.First();
        Assert.Equal("right_hand_weapon_cube_0", weaponCubeNode.Name);
        var weaponProps = weaponCubeNode.Object!.Properties;
        Assert.Equal("PerFace", weaponProps["UV"].GetString());
        Assert.Equal(0.1, weaponProps["Inflate"].GetDouble(), precision: 4);
    }

    [Fact]
    public void BedrockGeometry_MultiCubeBone_CreatesDistinctChildNodes_ForEachCube()
    {
        string fixturePath = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parser = new BedrockGeometryParser();
        var doc = parser.ParseFile(fixturePath).Document;

        // Add a second cube to the root bone to explicitly test multi-cube bones
        var rootBone = doc.Geometries[0].Bones.First(b => b.Name == "root");
        rootBone.Cubes.Add(new BedrockCube
        {
            Origin = new Vector3D(0, 10, 0),
            Size = new Vector3D(4, 4, 4),
            Uv = BedrockCubeUv.CreateBoxUv(0, 0)
        });

        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(doc);

        var rootNode = scene.Graph.Traverse().First(n => n.Name == "root");
        var childCubes = rootNode.Children.Where(c => c.Name.StartsWith("root_cube_")).ToList();

        // root bone now has 2 cubes
        Assert.Equal(2, childCubes.Count);
        Assert.NotEqual(childCubes[0].Id, childCubes[1].Id);
        Assert.NotEqual(childCubes[0].Object!.Id, childCubes[1].Object!.Id);
        Assert.NotEqual(childCubes[0].Object!.Meshes[0].Id, childCubes[1].Object!.Meshes[0].Id);
        Assert.Equal("root_cube_0", childCubes[0].Name);
        Assert.Equal("root_cube_1", childCubes[1].Name);
    }

    #endregion

    #region 2. RaycastPicker Exact Voxel Coordinates and Block State

    [Fact]
    public void RaycastPicker_CalculatesExactVoxelCoordinate_AndBlockState_WhenCastingRaysAtVoxelGrid()
    {
        var picker = new RaycastPicker();
        var scene = new Scene3D { Name = "SchematicPickerScene" };

        var grid = new VoxelGrid("SchematicGrid");

        // Define realistic Minecraft block states
        var stoneState = new BlockState("minecraft", "stone");
        var stairsProps = new Dictionary<string, string>
        {
            ["facing"] = "south",
            ["half"] = "bottom",
            ["shape"] = "straight",
            ["waterlogged"] = "false"
        };
        var stairsState = new BlockState("minecraft", "oak_stairs", stairsProps);
        var diamondState = new BlockState("minecraft", "diamond_block");
        var redstoneLampProps = new Dictionary<string, string> { ["lit"] = "true" };
        var lampState = new BlockState("minecraft", "redstone_lamp", redstoneLampProps);

        int palStone = grid.Palette.GetOrAdd(stoneState);
        int palStairs = grid.Palette.GetOrAdd(stairsState);
        int palDiamond = grid.Palette.GetOrAdd(diamondState);
        int palLamp = grid.Palette.GetOrAdd(lampState);

        // Place blocks at specific integer voxel positions
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), palStone, stoneState));
        grid.SetVoxel(new Voxel(new VoxelPosition(2, 0, 1), palStairs, stairsState));
        grid.SetVoxel(new Voxel(new VoxelPosition(2, 1, 1), palDiamond, diamondState));
        grid.SetVoxel(new Voxel(new VoxelPosition(5, 3, 2), palLamp, lampState));

        var voxelNode = new SceneNode("SchematicNode", new SceneObject("SchematicObj") { VoxelGrids = { grid } });
        scene.Graph.AddNode(scene.Graph.Root, voxelNode);

        // 1. Ray along -Z hitting oak_stairs front face at (2, 0, 1)
        // Voxel is at X: [2, 3], Y: [0, 1], Z: [1, 2]
        // Ray origin at (2.5, 0.5, 10), pointing (0, 0, -1)
        var rayFrontZ = new Ray3D(new Vector3D(2.5, 0.5, 10), new Vector3D(0, 0, -1));
        var hitStairs = picker.Pick(scene, rayFrontZ);

        Assert.NotNull(hitStairs);
        Assert.Equal(voxelNode.Id, hitStairs.NodeId);
        Assert.Null(hitStairs.MeshId);
        Assert.Equal(new VoxelPosition(2, 0, 1), hitStairs.VoxelCoord);
        Assert.Equal("minecraft:oak_stairs", hitStairs.BlockIdentifier);
        Assert.NotNull(hitStairs.BlockStateString);
        Assert.Contains("minecraft:oak_stairs", hitStairs.BlockStateString);
        Assert.Contains("facing=south", hitStairs.BlockStateString);
        Assert.Contains("half=bottom", hitStairs.BlockStateString);
        Assert.Equal(new Vector3D(0, 0, 1), hitStairs.Normal);
        Assert.Equal(new Vector3D(2.5, 0.5, 2.0), hitStairs.HitPoint);
        Assert.True(Math.Abs(hitStairs.Distance - 8.0) < 1e-4);

        // 2. Ray along -Y hitting top face of diamond_block at (2, 1, 1)
        // Voxel is at X: [2, 3], Y: [1, 2], Z: [1, 2]
        // Ray origin at (2.5, 10, 1.5), pointing (0, -1, 0)
        var rayTopY = new Ray3D(new Vector3D(2.5, 10, 1.5), new Vector3D(0, -1, 0));
        var hitDiamond = picker.Pick(scene, rayTopY);

        Assert.NotNull(hitDiamond);
        Assert.Equal(voxelNode.Id, hitDiamond.NodeId);
        Assert.Equal(new VoxelPosition(2, 1, 1), hitDiamond.VoxelCoord);
        Assert.Equal("minecraft:diamond_block", hitDiamond.BlockIdentifier);
        Assert.Equal("minecraft:diamond_block", hitDiamond.BlockStateString);
        Assert.Equal(new Vector3D(0, 1, 0), hitDiamond.Normal);
        Assert.Equal(new Vector3D(2.5, 2.0, 1.5), hitDiamond.HitPoint);
        Assert.True(Math.Abs(hitDiamond.Distance - 8.0) < 1e-4);

        // 3. Ray hitting redstone lamp at (5, 3, 2) from +X along -X
        // Voxel is at X: [5, 6], Y: [3, 4], Z: [2, 3]
        // Ray origin at (15, 3.5, 2.5), pointing (-1, 0, 0)
        var rayLampX = new Ray3D(new Vector3D(15, 3.5, 2.5), new Vector3D(-1, 0, 0));
        var hitLamp = picker.Pick(scene, rayLampX);

        Assert.NotNull(hitLamp);
        Assert.Equal(new VoxelPosition(5, 3, 2), hitLamp.VoxelCoord);
        Assert.Equal("minecraft:redstone_lamp", hitLamp.BlockIdentifier);
        Assert.Equal("minecraft:redstone_lamp[lit=true]", hitLamp.BlockStateString);
        Assert.Equal(new Vector3D(1, 0, 0), hitLamp.Normal);
        Assert.Equal(new Vector3D(6.0, 3.5, 2.5), hitLamp.HitPoint);
        Assert.True(Math.Abs(hitLamp.Distance - 9.0) < 1e-4);

        // 4. Ray missing all voxels returns null
        var rayMiss = new Ray3D(new Vector3D(100, 100, 100), new Vector3D(0, 0, -1));
        var hitMiss = picker.Pick(scene, rayMiss);
        Assert.Null(hitMiss);
    }

    [Fact]
    public void RaycastPicker_TraversingAirVoxels_PassesThroughAndHitsSolidVoxelBehind()
    {
        var picker = new RaycastPicker();
        var scene = new Scene3D { Name = "AirPassScene" };
        var grid = new VoxelGrid("AirPassGrid");

        var airState = new BlockState("minecraft", "air");
        var goldState = new BlockState("minecraft", "gold_block");

        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 0), 0, airState));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 2), 0, airState));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 4), 1, goldState));

        var node = new SceneNode("VoxelNode", new SceneObject("VoxelObj") { VoxelGrids = { grid } });
        scene.Graph.AddNode(scene.Graph.Root, node);

        // Ray starts at Z=-5 and points along +Z through (1, 0, 0) and (1, 0, 2) to hit (1, 0, 4)
        var ray = new Ray3D(new Vector3D(1.5, 0.5, -5), new Vector3D(0, 0, 1));
        var hit = picker.Pick(scene, ray);

        Assert.NotNull(hit);
        Assert.Equal(new VoxelPosition(1, 0, 4), hit.VoxelCoord);
        Assert.Equal("minecraft:gold_block", hit.BlockIdentifier);
        Assert.Equal(new Vector3D(0, 0, -1), hit.Normal);
        Assert.Equal(new Vector3D(1.5, 0.5, 4.0), hit.HitPoint);
        Assert.True(Math.Abs(hit.Distance - 9.0) < 1e-4);
    }

    [Fact]
    public void RaycastPicker_RespectsSceneNodeTransform_WhenComputingCoordinates()
    {
        var picker = new RaycastPicker();
        var scene = new Scene3D { Name = "TransformedVoxelScene" };
        var grid = new VoxelGrid("TransformedGrid");

        var emeraldState = new BlockState("minecraft", "emerald_block");
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 1, emeraldState));

        var node = new SceneNode("VoxelNode", new SceneObject("VoxelObj") { VoxelGrids = { grid } })
        {
            LocalTransform = new Transform3D(new Vector3D(10, 5, -20), QuaternionD.Identity, Vector3D.One)
        };
        scene.Graph.AddNode(scene.Graph.Root, node);

        // Ray directed towards the world-shifted voxel at (10.5, 5.5, -10) pointing along -Z
        var ray = new Ray3D(new Vector3D(10.5, 5.5, -10), new Vector3D(0, 0, -1));
        var hit = picker.Pick(scene, ray);

        Assert.NotNull(hit);
        Assert.Equal(node.Id, hit.NodeId);
        // VoxelCoord is in grid-local integer coordinates
        Assert.Equal(new VoxelPosition(0, 0, 0), hit.VoxelCoord);
        Assert.Equal("minecraft:emerald_block", hit.BlockIdentifier);
        // World hit point accounts for the node translation
        Assert.Equal(new Vector3D(10.5, 5.5, -19.0), hit.HitPoint);
        Assert.True(Math.Abs(hit.Distance - 9.0) < 1e-4);
    }

    #endregion

    #region 3. Bedrock Export Preserves All Cubes and Bones with Child Nodes

    [Fact]
    public void BedrockGeometry_ExportRoundtrip_PreservesAllCubesAndBones_WithIndividualChildNodes()
    {
        string fixturePath = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parser = new BedrockGeometryParser();
        var origDoc = parser.ParseFile(fixturePath).Document;
        var origGeo = origDoc.Geometries[0];

        // 1. Convert to Scene3D
        var toSceneConverter = new BedrockGeometryToSceneConverter();
        var (scene, toSceneReport) = toSceneConverter.Convert(origDoc);

        // Verify child cube nodes exist in USM scene
        var weaponNode = scene.Graph.Traverse().First(n => n.Name == "right_hand_weapon");
        Assert.NotEmpty(weaponNode.Children);
        Assert.True(weaponNode.Children.All(c => c.Name.Contains("cube")));

        // 2. Export back to BedrockGeometryDocument
        var fromSceneConverter = new SceneToBedrockGeometryConverter();
        var (docBack, fromSceneReport) = fromSceneConverter.Convert(scene);

        Assert.NotNull(docBack);
        Assert.Single(docBack.Geometries);
        var exportedGeo = docBack.Geometries[0];

        // Verify bone count & names
        Assert.Equal(origGeo.Bones.Count, exportedGeo.Bones.Count);
        for (int i = 0; i < origGeo.Bones.Count; i++)
        {
            Assert.Equal(origGeo.Bones[i].Name, exportedGeo.Bones[i].Name);
            Assert.Equal(origGeo.Bones[i].Parent, exportedGeo.Bones[i].Parent);
        }

        // Verify total cubes preserved
        int totalCubesBack = exportedGeo.Bones.Sum(b => b.Cubes.Count);
        Assert.Equal(toSceneReport.CubeCount, totalCubesBack);

        // Verify individual cubes on right_hand_weapon
        var weaponBoneBack = exportedGeo.Bones.First(b => b.Name == "right_hand_weapon");
        Assert.Single(weaponBoneBack.Cubes);
        var cubeBack = weaponBoneBack.Cubes[0];

        var origWeaponCube = origGeo.Bones.First(b => b.Name == "right_hand_weapon").Cubes[0];
        Assert.Equal(origWeaponCube.Origin.X, cubeBack.Origin.X, precision: 2);
        Assert.Equal(origWeaponCube.Origin.Y, cubeBack.Origin.Y, precision: 2);
        Assert.Equal(origWeaponCube.Origin.Z, cubeBack.Origin.Z, precision: 2);
        Assert.Equal(origWeaponCube.Size.X, cubeBack.Size.X, precision: 2);
        Assert.Equal(origWeaponCube.Size.Y, cubeBack.Size.Y, precision: 2);
        Assert.Equal(origWeaponCube.Size.Z, cubeBack.Size.Z, precision: 2);
    }

    [Fact]
    public void BedrockGeometry_SynthesizeFromRawUsmScene_ExtractsAllCubesFromChildNodes_WithoutSnapshot()
    {
        string fixturePath = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parser = new BedrockGeometryParser();
        var origDoc = parser.ParseFile(fixturePath).Document;

        var toSceneConverter = new BedrockGeometryToSceneConverter();
        var (scene, toSceneReport) = toSceneConverter.Convert(origDoc);

        // Strip snapshot metadata extension to force pure synthesis mode
        scene.Metadata.Extensions.Remove(BedrockGeometryToSceneConverter.ExtensionNamespace);

        var fromSceneConverter = new SceneToBedrockGeometryConverter();
        var (docSynth, synthReport) = fromSceneConverter.Convert(scene);

        Assert.NotNull(docSynth);
        Assert.Single(docSynth.Geometries);
        var synthGeo = docSynth.Geometries[0];

        Assert.Equal(origDoc.Geometries[0].Bones.Count, synthGeo.Bones.Count);

        int totalCubesSynth = synthGeo.Bones.Sum(b => b.Cubes.Count);
        Assert.True(totalCubesSynth > 0, "Synthesized document must extract cubes from child SceneNodes");
        Assert.Equal(toSceneReport.CubeCount, totalCubesSynth);

        // Verify right_arm bone cubes synthesized from child nodes
        var armBone = synthGeo.Bones.First(b => b.Name == "right_arm");
        Assert.NotEmpty(armBone.Cubes);
        Assert.True(armBone.Cubes[0].HasValidOrigin);
        Assert.True(armBone.Cubes[0].HasValidSize);
    }

    [Fact]
    public void BedrockGeometry_SynchronizesChildCubeTransformDelta_ToExportedCubeOrigin()
    {
        string fixturePath = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parser = new BedrockGeometryParser();
        var origDoc = parser.ParseFile(fixturePath).Document;

        var toSceneConverter = new BedrockGeometryToSceneConverter();
        var (scene, _) = toSceneConverter.Convert(origDoc);

        // Translate the child cube node in USM coordinates by (+3 on X, +4 on Y, -5 on Z)
        var weaponNode = scene.Graph.Traverse().First(n => n.Name == "right_hand_weapon");
        var cubeNode = weaponNode.Children.First(c => c.Name.Contains("cube"));
        cubeNode.LocalTransform = new Transform3D(new Vector3D(3, 4, -5), QuaternionD.Identity, Vector3D.One);

        var fromSceneConverter = new SceneToBedrockGeometryConverter();
        var (docBack, _) = fromSceneConverter.Convert(scene);

        var weaponBone = docBack.Geometries[0].Bones.First(b => b.Name == "right_hand_weapon");
        var cube = weaponBone.Cubes[0];

        // USM delta (+3, +4, -5) -> Bedrock delta (-3, +4, -5)
        // Original origin was (-7, 12, -8) -> Expected (-10, 16, -13)
        Assert.Equal(-10.0, cube.Origin.X, precision: 2);
        Assert.Equal(16.0, cube.Origin.Y, precision: 2);
        Assert.Equal(-13.0, cube.Origin.Z, precision: 2);
    }

    #endregion

    #region 4. Camera Framing & Workspace Synchronization

    [Fact]
    public void CameraService_FocusBounds_CalculatesAccurateTargetAndDistance_ForIndividualBlock()
    {
        var cameraService = new CameraService();

        // 1x1x1 block at (4, 10, -6)
        var blockMin = new Vector3(4f, 10f, -6f);
        var blockMax = new Vector3(5f, 11f, -5f);
        var expectedCenter = new Vector3(4.5f, 10.5f, -5.5f);

        var camState = cameraService.FocusBounds(blockMin, blockMax, fieldOfViewDegrees: 45f);

        Assert.Equal(expectedCenter.X, camState.Target.X, precision: 3);
        Assert.Equal(expectedCenter.Y, camState.Target.Y, precision: 3);
        Assert.Equal(expectedCenter.Z, camState.Target.Z, precision: 3);

        // Distance from target to position must be greater than block radius
        float dist = Vector3.Distance(camState.Position, camState.Target);
        Assert.True(dist > 1.0f);
    }

    [Fact]
    public void BedrockWorkspace_SelectCube_UpdatesInspectionAndRaisesEvent()
    {
        string fixturePath = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parser = new BedrockGeometryParser();
        var doc = parser.ParseFile(fixturePath).Document;
        var converter = new BedrockGeometryToSceneConverter();
        var (scene, _) = converter.Convert(doc);

        var vm = new BedrockWorkspaceViewModel();
        vm.LoadFromScene(scene);

        string? selectedNodeId = null;
        string? highlightedNodeId = null;
        vm.CubeSelected += (s, id) => selectedNodeId = id;
        vm.CubeHighlightRequested += (s, id) => highlightedNodeId = id;

        // Find the right_hand_weapon bone in VM tree
        var weaponBone = vm.Geometries[0].Bones
            .SelectMany(GetAllBonesRecursive)
            .First(b => b.Name == "right_hand_weapon");

        Assert.Single(weaponBone.Cubes);
        var cubeItem = weaponBone.Cubes[0];

        vm.SelectCube(cubeItem);

        Assert.Same(cubeItem, vm.SelectedCube);
        Assert.True(cubeItem.IsSelected);
        Assert.NotNull(selectedNodeId);
        Assert.Equal(cubeItem.NodeId?.ToString(), selectedNodeId);
        Assert.Equal(cubeItem.NodeId?.ToString(), highlightedNodeId);

        // Verify Inspector properties on cube
        Assert.Equal("right_hand_weapon_cube_0", cubeItem.Name);
        Assert.Equal("right_hand_weapon", cubeItem.BoneName);
        Assert.Equal("PerFace", cubeItem.Uv);
        Assert.Equal(0.1f, cubeItem.Inflate, precision: 2);
    }

    private static IEnumerable<BedrockBoneNodeViewModel> GetAllBonesRecursive(BedrockBoneNodeViewModel bone)
    {
        yield return bone;
        foreach (var child in bone.Children)
        {
            foreach (var sub in GetAllBonesRecursive(child))
            {
                yield return sub;
            }
        }
    }

    #endregion
}

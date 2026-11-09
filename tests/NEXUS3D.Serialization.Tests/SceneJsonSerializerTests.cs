using System.Text.Json;
using NEXUS3D.Application.Serialization;
using NEXUS3D.Domain.Animations;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Factories;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Domain.Textures;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Infrastructure.Serialization.Json;

namespace NEXUS3D.Serialization.Tests;

public sealed class SceneJsonSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_EmptyScene_ShouldRoundTripWithFullFidelity()
    {
        var serializer = new SceneJsonSerializer();
        var factory = new SceneFactory();

        Scene3D original = factory.Create("EmptyRoundTripScene");
        SceneSerializationResult serResult = serializer.SerializeToString(original);

        Assert.True(serResult.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(serResult.SerializedJson));
        Assert.Equal(Scene3D.CurrentSchemaVersion, serResult.SchemaVersion);

        SceneSerializationResult deserResult = serializer.DeserializeFromString(serResult.SerializedJson!);
        Assert.True(deserResult.IsSuccess);
        Assert.NotNull(deserResult.Scene);

        Scene3D restored = deserResult.Scene!;
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Graph.Root.Id, restored.Graph.Root.Id);
        Assert.True(restored.Validate().IsValid);
    }

    [Fact]
    public async Task SerializeAndDeserialize_ComplexScene_ShouldPreserveAllUsmComponentsAndExtensions()
    {
        var serializer = new SceneJsonSerializer();
        var sceneFactory = new SceneFactory();
        var nodeFactory = new SceneNodeFactory();
        var meshFactory = new MeshFactory();

        Scene3D scene = sceneFactory.Create("ComplexUsmScene");
        scene.Metadata.Author = "NEXUS Architect";
        scene.Metadata.Description = "Full USM round-trip test";
        scene.Metadata.Source.OriginalFileName = "castle.schem";
        scene.Metadata.Source.OriginalFormatId = "sponge_schematic_v3";

        var ext = new ExtensionData("minecraft:sponge_schematic");
        ext.Values["DataVersion"] = JsonDocument.Parse("3465").RootElement.Clone();
        scene.Metadata.AddExtension(ext);

        scene.Metadata.PreservedRawData.Add(new RawDataContainer
        {
            Name = "CustomBiomeChunk",
            SourceFormat = "schem",
            PayloadKind = RawPayloadKind.Binary,
            BinaryPayload = [0x0A, 0x00, 0x01, 0x7F]
        });

        scene.Metadata.LossReport.RecordSupported("VoxelPalette", "100% preserved");
        scene.Metadata.LossReport.RecordPreservedRaw("CustomBiomeChunk", "Binary payload preserved");

        // Texture & Material
        var texture = new TextureData("stone_bricks.png", 32, 32, new TextureReference
        {
            StorageKind = TextureStorageKind.External,
            RelativePath = "textures/stone_bricks.png"
        });
        scene.Textures.Add(texture);

        var material = new MaterialData("StoneBrickMat")
        {
            BaseColor = new MaterialColor(0.8, 0.8, 0.8, 1.0),
            Roughness = 0.9,
            Metallic = 0.0
        };
        material.TextureSlots["Albedo"] = new TextureSlot(texture.Id);
        scene.Materials.Add(material);

        // Mesh Node
        MeshData boxMesh = meshFactory.CreateBox("TowerMesh", new Vector3D(4, 8, 4), material.Id);
        boxMesh.Submeshes.Add(new MeshSubmesh("TowerWalls", 0, 6, material.Id));
        SceneNode meshNode = nodeFactory.CreateMeshNode(
            "TowerNode",
            boxMesh,
            new Transform3D(new Vector3D(10, 0, -5), QuaternionD.FromEulerAnglesDegrees(0, 45, 0), Vector3D.One));
        scene.Graph.AddNode(scene.Graph.Root, meshNode);

        // Voxel Node as child of Mesh Node
        var voxelGrid = new VoxelGrid("CastleWallGrid");
        int brickIdx = voxelGrid.Palette.GetOrAdd(
            new BlockState("minecraft", "stone_bricks", new Dictionary<string, string> { ["variant"] = "mossy" }),
            material.Id);
        voxelGrid.SetVoxel(new Voxel(
            new VoxelPosition(1, 2, 3),
            brickIdx,
            blockEntity: new BlockEntityData("minecraft:banner")));

        var layer = new VoxelLayer("MainWallLayer");
        layer.Positions.Add(new VoxelPosition(1, 2, 3));
        voxelGrid.Layers.Add(layer);

        SceneNode voxelNode = nodeFactory.CreateVoxelNode("VoxelWallNode", voxelGrid);
        scene.Graph.AddNode(meshNode, voxelNode);

        // Skeleton & Animation
        var skeleton = new SkeletonData("GateRig");
        var rootBone = new BoneData("gate_root");
        var hingeBone = new BoneData("gate_hinge", rootBone.Id);
        skeleton.RootBoneId = rootBone.Id;
        skeleton.Bones.AddRange([rootBone, hingeBone]);
        scene.Skeletons.Add(skeleton);

        var animation = new AnimationData("gate_open", duration: 2.0, frameRate: 24.0);
        animation.Tracks.Add(new AnimationTrack
        {
            TargetId = hingeBone.Id,
            Channel = AnimationChannel.Rotation,
            Keyframes =
            [
                AnimationKeyframe.ForRotation(0.0, QuaternionD.Identity),
                AnimationKeyframe.ForRotation(2.0, QuaternionD.FromEulerAnglesDegrees(0, 90, 0))
            ]
        });
        animation.Events.Add(new AnimationEvent { Time = 1.0, Name = "PlayGateSound", Parameter = "creak_heavy" });
        scene.Animations.Add(animation);

        // Stream round-trip
        using var memoryStream = new MemoryStream();
        var saveResult = await serializer.SerializeToStreamAsync(scene, memoryStream);
        Assert.True(saveResult.IsSuccess);

        memoryStream.Position = 0;
        var loadResult = await serializer.DeserializeFromStreamAsync(memoryStream);
        Assert.True(loadResult.IsSuccess);

        Scene3D restored = loadResult.Scene!;
        Assert.True(restored.Validate().IsValid);

        SceneStatistics origStats = scene.CalculateStatistics();
        SceneStatistics restoredStats = restored.CalculateStatistics();
        Assert.Equal(origStats.NodeCount, restoredStats.NodeCount);
        Assert.Equal(origStats.MeshCount, restoredStats.MeshCount);
        Assert.Equal(origStats.TotalVertexCount, restoredStats.TotalVertexCount);
        Assert.Equal(origStats.TotalFaceCount, restoredStats.TotalFaceCount);
        Assert.Equal(origStats.VoxelGridCount, restoredStats.VoxelGridCount);
        Assert.Equal(origStats.TotalVoxelCount, restoredStats.TotalVoxelCount);
        Assert.Equal(origStats.MaterialCount, restoredStats.MaterialCount);
        Assert.Equal(origStats.TextureCount, restoredStats.TextureCount);
        Assert.Equal(origStats.SkeletonCount, restoredStats.SkeletonCount);
        Assert.Equal(origStats.AnimationCount, restoredStats.AnimationCount);
        Assert.Equal(origStats.PreservedRawDataCount, restoredStats.PreservedRawDataCount);
        Assert.Equal(origStats.WorldBounds.Min.X, restoredStats.WorldBounds.Min.X, 6);
        Assert.Equal(origStats.WorldBounds.Max.X, restoredStats.WorldBounds.Max.X, 6);
    }

    [Fact]
    public void Deserialize_InvalidSchema_CorruptedJson_AndLimits_ShouldReturnExpectedUsmErrorCodes()
    {
        var serializer = new SceneJsonSerializer();
        var sceneFactory = new SceneFactory();

        // 1. Corrupted JSON -> USM017 (SerializationFailed)
        var corrupted = serializer.DeserializeFromString("{ not_valid_json ");
        Assert.False(corrupted.IsSuccess);
        Assert.Equal(UsmErrorCodes.SerializationFailed, corrupted.Error?.Code);

        // 2. Unsupported Schema Version -> USM016 (UnsupportedSchemaVersion)
        Scene3D validScene = sceneFactory.Create("SchemaTest");
        validScene.SchemaVersion = 999;
        string v999Json = serializer.SerializeToString(validScene).SerializedJson!;
        var unsupported = serializer.DeserializeFromString(v999Json);
        Assert.False(unsupported.IsSuccess);
        Assert.Equal(UsmErrorCodes.UnsupportedSchemaVersion, unsupported.Error?.Code);

        // 3. Max payload bytes exceeded -> USM018 (DataLimitExceeded)
        validScene.SchemaVersion = Scene3D.CurrentSchemaVersion;
        var smallLimitOptions = new SceneSerializationOptions { MaxPayloadBytes = 32 };
        var payloadExceeded = serializer.SerializeToString(validScene, smallLimitOptions);
        Assert.False(payloadExceeded.IsSuccess);
        Assert.Equal(UsmErrorCodes.DataLimitExceeded, payloadExceeded.Error?.Code);

        // 4. Max graph depth exceeded -> USM018 (DataLimitExceeded)
        SceneNode current = validScene.Graph.Root;
        for (int i = 0; i < 10; i++)
        {
            var next = new SceneNode($"Depth_{i}");
            validScene.Graph.AddNode(current, next);
            current = next;
        }

        var depthLimitOptions = new SceneSerializationOptions { MaxDepth = 5 };
        var depthExceeded = serializer.SerializeToString(validScene, depthLimitOptions);
        Assert.False(depthExceeded.IsSuccess);
        Assert.Equal(UsmErrorCodes.DataLimitExceeded, depthExceeded.Error?.Code);
    }
}

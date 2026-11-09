using System.Text.Json;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Factories;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Textures;

namespace NEXUS3D.Domain.Tests.Materials;

public sealed class MaterialTextureAndMetadataTests
{
    [Fact]
    public void SceneValidator_ShouldDetectMissingMaterialAndTextureReferences()
    {
        var sceneFactory = new SceneFactory();
        var meshFactory = new MeshFactory();
        var nodeFactory = new SceneNodeFactory();

        var scene = sceneFactory.Create("MatTestScene");

        Guid missingMaterialId = Guid.NewGuid();
        var mesh = meshFactory.CreateBox("BoxWithMissingMat", Vector3D.One, missingMaterialId);
        scene.Graph.AddNode(scene.Graph.Root, nodeFactory.CreateMeshNode("BoxNode", mesh));

        var result1 = scene.Validate();
        Assert.False(result1.IsValid);
        Assert.Contains(result1.Issues, i => i.Code == UsmErrorCodes.MaterialReferenceNotFound);

        // Now add the material, but with a TextureSlot pointing to a missing texture
        var mat = new MaterialData("BrickMaterial") { Id = missingMaterialId };
        mat.TextureSlots["Albedo"] = new TextureSlot(Guid.NewGuid());
        scene.Materials.Add(mat);

        var result2 = scene.Validate();
        Assert.False(result2.IsValid);
        Assert.DoesNotContain(result2.Issues, i => i.Code == UsmErrorCodes.MaterialReferenceNotFound);
        Assert.Contains(result2.Issues, i => i.Code == UsmErrorCodes.TextureReferenceNotFound);

        // Now add the texture so all references resolve
        Guid validTexId = mat.TextureSlots["Albedo"].TextureId;
        var tex = new TextureData("brick.png", 64, 64, new TextureReference
        {
            StorageKind = TextureStorageKind.External,
            RelativePath = "textures/brick.png"
        })
        {
            Id = validTexId
        };
        scene.Textures.Add(tex);

        var result3 = scene.Validate();
        Assert.True(result3.IsValid);
    }

    [Fact]
    public void Metadata_ExtensionNamespace_RawDataLimits_AndLossReportCategories_ShouldEnforceRules()
    {
        var metadata = new SceneMetadata { Title = "MetadataTest" };

        // Invalid extension namespace (missing colon) throws DomainException USM015
        var badExt = new ExtensionData("invalid_namespace_without_colon");
        var ex = Assert.Throws<DomainException>(() => metadata.AddExtension(badExt));
        Assert.Equal(UsmErrorCodes.InvalidMetadata, ex.ErrorCode);

        // Valid extension namespace succeeds
        var goodExt = new ExtensionData("minecraft:sponge_schematic");
        goodExt.Values["DataVersion"] = JsonDocument.Parse("3465").RootElement.Clone();
        metadata.AddExtension(goodExt);
        Assert.Single(metadata.Extensions);

        // RawDataContainer size limit check
        var oversizedRaw = new RawDataContainer
        {
            Name = "OversizedChunk",
            SourceFormat = "schem",
            PayloadKind = RawPayloadKind.Binary,
            MaxAllowedBytes = 16,
            BinaryPayload = new byte[64]
        };
        Assert.False(oversizedRaw.IsWithinSizeLimit());
        Assert.False(oversizedRaw.IsValid());

        // LossReport strictly separates PreservedRaw from Lost
        var report = new LossReport();
        report.RecordSupported("MeshVertices", "100% preserved");
        report.RecordPreservedRaw("CustomBlockEntityNbt", "Stored in RawDataContainer");
        Assert.False(report.HasDataLoss);
        Assert.Single(report.SupportedItems);
        Assert.Single(report.PreservedRawItems);

        report.RecordLost("UnsupportedProceduralShader", "Dropped during import");
        Assert.True(report.HasDataLoss);
        Assert.Single(report.LostItems);
    }
}

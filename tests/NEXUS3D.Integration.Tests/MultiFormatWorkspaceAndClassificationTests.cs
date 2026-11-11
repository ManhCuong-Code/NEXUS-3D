using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Application.UseCases;
using NEXUS3D.Conversion.Engine.Services;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.BedrockGeometry;
using NEXUS3D.Formats.Obj;
using NEXUS3D.Formats.Schematic;
using NEXUS3D.Formats.VoxelJson;
using NEXUS3D.Infrastructure.Formats;
using NEXUS3D.Rendering.Profiles;
using Xunit;

namespace NEXUS3D.Integration.Tests;

public sealed class MultiFormatWorkspaceAndClassificationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileClassificationService _classificationService;
    private readonly ConversionCompatibilityService _compatibilityService;

    public MultiFormatWorkspaceAndClassificationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"Nexus3D_WorkspaceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var registry = new FormatRegistry(
        [
            new ObjFormatDescriptor(),
            new SchematicFormatDescriptor(),
            new VoxelJsonFormatDescriptor(),
            new BedrockGeometryFormatDescriptor()
        ]);

        _classificationService = new FileClassificationService(registry, NullLogger<FileClassificationService>.Instance);

        var capabilityRegistry = new FormatCapabilityRegistry(NullLogger<FormatCapabilityRegistry>.Instance);
        var lossAnalyzer = new DataLossAnalyzer(capabilityRegistry, NullLogger<DataLossAnalyzer>.Instance);
        _compatibilityService = new ConversionCompatibilityService(registry, lossAnalyzer, NullLogger<ConversionCompatibilityService>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }

    [Fact]
    public void ClassifyFile_ObjWithNormalsAndMtl_ReturnsPolygonMeshDefinite()
    {
        string objPath = Path.Combine(_tempDir, "cube.obj");
        File.WriteAllText(objPath, @"mtllib cube.mtl
v 0 0 0
v 1 0 0
v 1 1 0
vn 0 0 1
vt 0 0
f 1/1/1 2/1/1 3/1/1
");
        var report = _classificationService.ClassifyFile(objPath);

        Assert.Equal(FormatCategory.PolygonMesh, report.Category);
        Assert.Equal(ModelFormatType.Obj, report.FormatType);
        Assert.Equal(ConfidenceLevel.Definite, report.Confidence);
        Assert.True(report.CanOpen);
        Assert.Contains(report.ReadableComponents, c => c.Contains("Polygonal Faces"));
    }

    [Fact]
    public void ClassifyFile_BedrockGeometryJson_ReturnsBedrockGeometryDefinite()
    {
        string geoPath = Path.Combine(_tempDir, "entity.geo.json");
        File.WriteAllText(geoPath, @"{
  ""format_version"": ""1.12.0"",
  ""minecraft:geometry"": [
    {
      ""description"": {
        ""identifier"": ""geometry.humanoid"",
        ""texture_width"": 64,
        ""texture_height"": 64
      },
      ""bones"": [
        { ""name"": ""root"", ""pivot"": [0, 0, 0] }
      ]
    }
  ]
}");
        var report = _classificationService.ClassifyFile(geoPath);

        Assert.Equal(FormatCategory.BedrockGeometry, report.Category);
        Assert.Equal(ModelFormatType.BedrockGeometry, report.FormatType);
        Assert.Equal(ConfidenceLevel.Definite, report.Confidence);
        Assert.Contains(report.ReadableComponents, c => c.Contains("Bone Hierarchy"));
    }

    [Fact]
    public void ClassifyFile_VoxelJson_ReturnsVoxelModelDefinite()
    {
        string voxelPath = Path.Combine(_tempDir, "model.voxel.json");
        File.WriteAllText(voxelPath, @"{
  ""version"": 1,
  ""palette"": [ ""#FF0000"", ""#00FF00"" ],
  ""voxels"": [
    { ""x"": 0, ""y"": 0, ""z"": 0, ""color"": 0 }
  ]
}");
        var report = _classificationService.ClassifyFile(voxelPath);

        Assert.Equal(FormatCategory.VoxelModel, report.Category);
        Assert.Equal(ModelFormatType.VoxelJson, report.FormatType);
        Assert.Equal(ConfidenceLevel.Definite, report.Confidence);
    }

    [Fact]
    public void CompatibilityService_FromMeshSource_RatingsComputedAccurately()
    {
        var scene = new Scene3D { Name = "TestMesh" };
        var node = new SceneNode("MeshNode");
        var obj = new SceneObject("Obj");
        var mesh = new MeshData();
        mesh.Vertices.Add(new Vertex(Vector3D.Zero));
        mesh.Faces.Add(new Face([0, 0, 0]));
        obj.Meshes.Add(mesh);
        node.Object = obj;
        scene.Graph.Root.AddChild(node);

        var targets = _compatibilityService.GetCompatibleTargets(scene, ModelFormatType.Obj);

        Assert.NotEmpty(targets);
        var objTarget = targets.FirstOrDefault(t => t.FormatType == ModelFormatType.Obj);
        Assert.NotNull(objTarget);
        Assert.Equal(CompatibilityRating.NativeLossless, objTarget.Rating);
        Assert.NotEmpty(objTarget.PreservedFeatures);
    }

    [Fact]
    public void ViewerProfileRegistry_ResolvesSpecializedProfilesCorrectly()
    {
        var meshProfile = ViewerProfileRegistry.GetProfile(ViewerProfileType.Mesh);
        var blockProfile = ViewerProfileRegistry.GetProfile(ViewerProfileType.Block);
        var voxelProfile = ViewerProfileRegistry.GetProfile(ViewerProfileType.Voxel);
        var bedrockProfile = ViewerProfileRegistry.GetProfile(ViewerProfileType.BedrockGeometry);

        Assert.Equal(ViewerProfileType.Mesh, meshProfile.ProfileType);
        Assert.Equal(ViewerProfileType.Block, blockProfile.ProfileType);
        Assert.Equal(ViewerProfileType.Voxel, voxelProfile.ProfileType);
        Assert.Equal(ViewerProfileType.BedrockGeometry, bedrockProfile.ProfileType);

        Assert.True(blockProfile.ShowGrid);
        Assert.True(bedrockProfile.ShowBones);
    }
}

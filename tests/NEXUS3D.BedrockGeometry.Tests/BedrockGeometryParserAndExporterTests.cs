using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.BedrockGeometry.Exporting;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.BedrockGeometry.Services;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.BedrockGeometry.Tests;

public sealed class BedrockGeometryParserAndExporterTests
{
    private static string GetFixturePath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "Json", fileName));

    [Fact]
    public void ParseFile_Legacy180Fixture_ParsesParentGeometryInheritanceBonesAndCubes()
    {
        var parser = new BedrockGeometryParser();
        string path = GetFixturePath("bedrock_1_8_0_legacy.geo.json");

        BedrockGeometryParseResult result = parser.ParseFile(path);

        Assert.False(result.HasErrors);
        Assert.Equal("1.8.0", result.Document.FormatVersion);
        Assert.Equal(BedrockGeometrySchemaFamily.Legacy180, result.Document.SchemaFamily);
        Assert.Single(result.Document.Geometries);

        var geo = result.Document.Geometries[0];
        Assert.Equal("geometry.legacy_golem", geo.Description.Identifier);
        Assert.Equal("geometry.humanoid", geo.ParentGeometryIdentifier);
        Assert.Equal(128, geo.Description.TextureWidth);
        Assert.Equal(128, geo.Description.TextureHeight);
        Assert.Equal(2, geo.Bones.Count);
        Assert.Equal(new Vector3D(5.0, 0.0, 0.0), geo.Bones[1].BindPoseRotation);
        Assert.True(geo.Bones[1].Reset);
    }

    [Fact]
    public void ParseFile_Modern1160Fixture_ResolvesOutOfOrderBoneHierarchyPerFaceUvLocatorsAndPolyMesh()
    {
        var parser = new BedrockGeometryParser();
        string path = GetFixturePath("bedrock_1_16_0_guardian.geo.json");

        BedrockGeometryParseResult result = parser.ParseFile(path);

        Assert.False(result.HasErrors);
        Assert.Equal("1.16.0", result.Document.FormatVersion);
        Assert.Equal(BedrockGeometrySchemaFamily.Modern1160Plus, result.Document.SchemaFamily);
        Assert.Single(result.Document.Geometries);

        var geo = result.Document.Geometries[0];
        Assert.Equal("geometry.nexus_guardian", geo.Description.Identifier);
        Assert.Equal("cape_guardian", geo.Cape);
        Assert.Equal(3, geo.Bones.Count);

        // In the JSON file, right_hand_weapon (child of right_arm) is declared BEFORE right_arm (child of root) and root!
        IReadOnlyList<BedrockBone> topoBones = geo.GetTopologicalBones();
        Assert.Equal("root", topoBones[0].Name);
        Assert.Equal("right_arm", topoBones[1].Name);
        Assert.Equal("right_hand_weapon", topoBones[2].Name);

        BedrockBone weaponBone = geo.Bones[0];
        Assert.Equal("q.item_slot_to_bone_name(c.item_slot)", weaponBone.Binding);
        Assert.Equal(2, weaponBone.Locators.Count);
        Assert.False(weaponBone.Locators["weapon_tip"].IsObjectForm);
        Assert.True(weaponBone.Locators["spell_emitter"].IsObjectForm);
        Assert.Equal(new Vector3D(0, 90, 0), weaponBone.Locators["spell_emitter"].Rotation);
        Assert.True(weaponBone.Locators["spell_emitter"].IgnoreInheritedScale);
        Assert.True(weaponBone.Locators["spell_emitter"].ExtensionData.TryGet("particle_channel", out _));

        BedrockCube weaponCube = weaponBone.Cubes[0];
        Assert.Equal(BedrockUvMode.PerFaceUv, weaponCube.Uv!.Mode);
        Assert.Equal(6, weaponCube.Uv.Faces.Count);
        Assert.Equal("blade_glow", weaponCube.Uv.Faces["north"].MaterialInstance);
        Assert.Equal(90, weaponCube.Uv.Faces["east"].UvRotation);

        BedrockBone rootBone = geo.Bones[2];
        Assert.NotNull(rootBone.PolyMesh);
        Assert.Equal(3, rootBone.PolyMesh!.Positions.Count);
        Assert.Single(rootBone.PolyMesh.Polys);
    }

    [Fact]
    public void Parse_BoneHierarchyCycleAndMissingParent_DetectsAndReportsJson009Error()
    {
        const string cycleJson = """
        {
          "format_version": "1.12.0",
          "minecraft:geometry": [
            {
              "description": { "identifier": "geometry.cycle_test", "texture_width": 64, "texture_height": 64 },
              "bones": [
                { "name": "bone_a", "parent": "bone_b", "pivot": [0, 0, 0] },
                { "name": "bone_b", "parent": "bone_a", "pivot": [0, 5, 0] }
              ]
            }
          ]
        }
        """;

        var parser = new BedrockGeometryParser();
        var cycleEx = Assert.Throws<JsonFormatException>(() =>
            parser.Parse(cycleJson, new BedrockGeometryReadOptions { StrictMode = true }));
        Assert.Equal(JsonFormatErrorCodes.BrokenReferenceOrHierarchy, cycleEx.ErrorCode);

        const string missingParentJson = """
        {
          "format_version": "1.12.0",
          "minecraft:geometry": [
            {
              "description": { "identifier": "geometry.missing_parent", "texture_width": 64, "texture_height": 64 },
              "bones": [
                { "name": "child_bone", "parent": "ghost_parent", "pivot": [0, 0, 0] }
              ]
            }
          ]
        }
        """;

        var missingParentEx = Assert.Throws<JsonFormatException>(() =>
            parser.Parse(missingParentJson, new BedrockGeometryReadOptions { StrictMode = true }));
        Assert.Equal(JsonFormatErrorCodes.BrokenReferenceOrHierarchy, missingParentEx.ErrorCode);
    }

    [Fact]
    public void Parse_MisplacedAnimationDocumentAndNegativeCubeSize_ThrowsExpectedJsonFormatException()
    {
        var parser = new BedrockGeometryParser();

        var animEx = Assert.Throws<JsonFormatException>(() =>
            parser.Parse("{ \"format_version\": \"1.10.0\", \"animations\": { \"animation.walk\": {} } }"));
        Assert.Equal(JsonFormatErrorCodes.MisplacedAnimationDocument, animEx.ErrorCode);

        const string negCubeJson = """
        {
          "format_version": "1.12.0",
          "minecraft:geometry": [
            {
              "description": { "identifier": "geometry.neg_cube", "texture_width": 64, "texture_height": 64 },
              "bones": [
                {
                  "name": "root",
                  "pivot": [0, 0, 0],
                  "cubes": [
                    { "origin": [0, 0, 0], "size": [4, -2, 4], "uv": [0, 0] }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var negSizeEx = Assert.Throws<JsonFormatException>(() =>
            parser.Parse(negCubeJson, new BedrockGeometryReadOptions { StrictMode = true }));
        Assert.Equal(JsonFormatErrorCodes.InvalidNumericValue, negSizeEx.ErrorCode);
    }

    [Fact]
    public void Export_AcrossLegacyAndModernVersions_ProducesValidDocumentsAndCompatibilityWarnings()
    {
        var parser = new BedrockGeometryParser();
        var exporter = new BedrockGeometryExporter();
        BedrockGeometryParseResult modern = parser.ParseFile(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));

        IReadOnlyList<BedrockGeometryDiagnostic> downgradeWarnings =
            exporter.EvaluateCompatibilityWarnings(modern.Document, "1.8.0");
        Assert.NotEmpty(downgradeWarnings);

        foreach (string version in new[] { "1.8.0", "1.12.0", "1.16.0", "1.21.0" })
        {
            string exported = exporter.ExportToString(modern.Document, new BedrockGeometryWriteOptions
            {
                TargetFormatVersion = version,
                PrettyPrint = true
            });

            BedrockGeometryParseResult reparsed = parser.Parse(exported);
            Assert.False(reparsed.HasErrors);
            Assert.Equal(version, reparsed.Document.FormatVersion);
            Assert.Single(reparsed.Document.Geometries);
            Assert.Equal(3, reparsed.Document.Geometries[0].Bones.Count);
        }
    }

    [Fact]
    public async Task Detector_IdentifiesLegacyAndModernBedrockGeometryStreams()
    {
        var detector = new BedrockGeometryFormatDetector();

        await using var legacyStream = File.OpenRead(GetFixturePath("bedrock_1_8_0_legacy.geo.json"));
        var legacyRes = await detector.DetectAsync(legacyStream, ".geo.json");
        Assert.True(legacyRes.IsRecognized);
        Assert.Equal(108, legacyRes.Version);

        await using var modernStream = File.OpenRead(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var modernRes = await detector.DetectAsync(modernStream, ".geo.json");
        Assert.True(modernRes.IsRecognized);
        Assert.Equal(116, modernRes.Version);
    }
}

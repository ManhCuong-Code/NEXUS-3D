using System.Text;
using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Exporting;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;
using NEXUS3D.Formats.VoxelJson.Services;

namespace NEXUS3D.VoxelJson.Tests;

public sealed class VoxelJsonParserAndExporterTests
{
    private static string GetFixturePath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "Json", fileName));

    [Fact]
    public void ParseFile_NexusVoxelGridV1Fixture_ParsesMetadataGridMaterialsLayersVoxelsAndExtensions()
    {
        var parser = new VoxelJsonParser();
        string path = GetFixturePath("voxel_nexus_v1.json");

        VoxelJsonParseResult result = parser.ParseFile(path);

        Assert.False(result.HasErrors);
        Assert.Equal(VoxelJsonDialect.NexusVoxelGridV1, result.DetectedDialect);
        Assert.Equal("Crystal Shrine", result.Document.Metadata.Name);
        Assert.Equal("NEXUS3D", result.Document.Metadata.Author);
        Assert.Equal(new Vector3D(16, 16, 16), result.Document.Grid.Dimensions);
        Assert.Equal(new Vector3D(0.5, 0.5, 0.5), result.Document.Grid.VoxelSize);
        Assert.Equal(new Vector2D(32, 32), result.Document.Grid.TextureSize);
        Assert.Equal(2, result.Document.Materials.Count);
        Assert.Equal(2, result.Document.Layers.Count);
        Assert.Equal(2, result.Document.Voxels.Count);

        VoxelRecord spire = result.Document.Voxels[1];
        Assert.Equal("crystal_spire", spire.Name);
        Assert.Equal(new Vector3D(4.5, 2, 4.5), spire.Position);
        Assert.Equal(new Vector3D(1, 3, 1), spire.Size);
        Assert.Equal(new Vector3D(0, 45, 0), spire.Rotation);
        Assert.Equal("crystal_core", spire.MaterialId);
        Assert.True(spire.ExtensionData.TryGet("customLightLevel", out JsonElement lightElem));
        Assert.Equal(15, lightElem.GetInt32());
        Assert.False(result.Document.RootExtensionData.IsEmpty);
    }

    [Fact]
    public void ParseFile_MinecraftBlockModelCuboidsFixture_ParsesCuboidsFacesRotationsAndGroups()
    {
        var parser = new VoxelJsonParser();
        string path = GetFixturePath("voxel_cuboid_lantern.json");

        VoxelJsonParseResult result = parser.ParseFile(path);

        Assert.False(result.HasErrors);
        Assert.Equal(VoxelJsonDialect.MinecraftBlockModelCuboids, result.DetectedDialect);
        Assert.False(result.Document.Metadata.AmbientOcclusion!.Value);
        Assert.Equal(2, result.Document.Materials.Count);
        Assert.Single(result.Document.Layers);
        Assert.Equal(2, result.Document.Voxels.Count);

        VoxelRecord body = result.Document.Voxels[0];
        Assert.Equal("lantern_body", body.Name);
        Assert.Equal(new Vector3D(5, 0, 5), body.Position);
        Assert.Equal(new Vector3D(6, 7, 6), body.Size);
        Assert.Equal(6, body.Faces.Count);
        Assert.Equal("lantern_root", body.LayerId);

        VoxelRecord chain = result.Document.Voxels[1];
        Assert.Equal("lantern_chain", chain.Name);
        Assert.Equal(new Vector3D(0, 45, 0), chain.Rotation);
        Assert.Equal(new Vector3D(8, 8, 8), chain.RotationOrigin);
        Assert.True(chain.Rescale);
    }

    [Fact]
    public void Parse_SparseVoxelArrayWithCompactTuplesAndObjects_Succeeds()
    {
        const string json = """
        {
          "name": "Sparse Castle",
          "dimensions": [8, 8, 8],
          "palette": ["#FF0000", "#00FF00"],
          "voxels": [
            [0, 0, 0, 0],
            [1, 0, 0, "#0000FF"],
            { "x": 2, "y": 3, "z": 4, "color": [255, 128, 0, 255], "size": [1, 2, 1], "type": "tower" }
          ]
        }
        """;

        var parser = new VoxelJsonParser();
        VoxelJsonParseResult result = parser.Parse(json);

        Assert.False(result.HasErrors);
        Assert.Equal(VoxelJsonDialect.SparseVoxelArray, result.DetectedDialect);
        Assert.Equal(3, result.Document.Voxels.Count);
        Assert.Equal("#FF0000", result.Document.Voxels[0].Color!.Value.ToHexString());
        Assert.Equal("#0000FF", result.Document.Voxels[1].Color!.Value.ToHexString());
        Assert.Equal(new Vector3D(1, 2, 1), result.Document.Voxels[2].Size);
        Assert.Equal("tower", result.Document.Voxels[2].VoxelType);
    }

    [Theory]
    [InlineData(DuplicateVoxelPolicy.KeepLastWithWarning, 1, "second")]
    [InlineData(DuplicateVoxelPolicy.KeepFirstWithWarning, 1, "first")]
    [InlineData(DuplicateVoxelPolicy.KeepAll, 2, "first")]
    public void Parse_DuplicateVoxelCoordinates_AppliesConfiguredPolicyAndRecordsWarning(
        DuplicateVoxelPolicy policy,
        int expectedCount,
        string expectedFirstType)
    {
        const string json = """
        {
          "voxels": [
            { "x": 1, "y": 2, "z": 3, "type": "first" },
            { "x": 1, "y": 2, "z": 3, "type": "second" }
          ]
        }
        """;

        var parser = new VoxelJsonParser();
        VoxelJsonParseResult result = parser.Parse(json, new VoxelJsonReadOptions
        {
            DuplicatePolicy = policy,
            StrictMode = false
        });

        Assert.Equal(1, result.DuplicateVoxelCount);
        Assert.True(result.HasWarnings);
        Assert.Equal(expectedCount, result.Document.Voxels.Count);
        Assert.Equal(expectedFirstType, result.Document.Voxels[0].VoxelType);
    }

    [Fact]
    public void Parse_DuplicateVoxelCoordinatesWithRejectPolicyInStrictMode_ThrowsJsonFormatException()
    {
        const string json = """
        {
          "voxels": [
            { "x": 5, "y": 5, "z": 5 },
            { "x": 5, "y": 5, "z": 5 }
          ]
        }
        """;

        var parser = new VoxelJsonParser();
        var ex = Assert.Throws<JsonFormatException>(() => parser.Parse(json, new VoxelJsonReadOptions
        {
            DuplicatePolicy = DuplicateVoxelPolicy.RejectWithError,
            StrictMode = true
        }));

        Assert.Equal(JsonFormatErrorCodes.DuplicateIdentifierOrCoordinate, ex.ErrorCode);
    }

    [Fact]
    public void Parse_MalformedAndInvalidInputs_ProducesExpectedErrorCodes()
    {
        var parser = new VoxelJsonParser();

        // 1. Empty JSON
        var emptyEx = Assert.Throws<JsonFormatException>(() => parser.Parse("   "));
        Assert.Equal(JsonFormatErrorCodes.EmptyOrInvalidRoot, emptyEx.ErrorCode);

        // 2. Broken JSON syntax
        var syntaxEx = Assert.Throws<JsonFormatException>(() => parser.Parse("{ \"voxels\": [ { \"x\": 1 "));
        Assert.Equal(JsonFormatErrorCodes.SyntaxError, syntaxEx.ErrorCode);

        // 3. Misplaced Bedrock Animation file
        var animEx = Assert.Throws<JsonFormatException>(() =>
            parser.Parse("{ \"format_version\": \"1.8.0\", \"animations\": { \"animation.test\": {} } }"));
        Assert.Equal(JsonFormatErrorCodes.MisplacedAnimationDocument, animEx.ErrorCode);

        // 4. Negative voxel size in strict mode
        var negSizeEx = Assert.Throws<JsonFormatException>(() =>
            parser.Parse("{ \"voxels\": [ { \"x\": 0, \"y\": 0, \"z\": 0, \"size\": [1, -2, 1] } ] }",
                new VoxelJsonReadOptions { StrictMode = true }));
        Assert.Equal(JsonFormatErrorCodes.InvalidNumericValue, negSizeEx.ErrorCode);

        // 5. Missing material reference in strict mode
        var brokenMatEx = Assert.Throws<JsonFormatException>(() =>
            parser.Parse("{ \"materials\": [{ \"id\": \"mat_a\", \"color\": \"#FFFFFF\" }], \"voxels\": [{ \"x\": 0, \"y\": 0, \"z\": 0, \"materialId\": \"non_existent\" }] }",
                new VoxelJsonReadOptions { StrictMode = true }));
        Assert.Equal(JsonFormatErrorCodes.BrokenReferenceOrHierarchy, brokenMatEx.ErrorCode);
    }

    [Fact]
    public void Export_AllThreeDialects_ProducesValidJsonThatReParsesCleanly()
    {
        var parser = new VoxelJsonParser();
        var exporter = new VoxelJsonExporter();
        VoxelJsonParseResult source = parser.ParseFile(GetFixturePath("voxel_nexus_v1.json"));

        foreach (VoxelJsonDialect dialect in new[]
                 {
                     VoxelJsonDialect.NexusVoxelGridV1,
                     VoxelJsonDialect.SparseVoxelArray,
                     VoxelJsonDialect.MinecraftBlockModelCuboids
                 })
        {
            string exportedJson = exporter.ExportToString(source.Document, new VoxelJsonWriteOptions
            {
                TargetDialect = dialect,
                PrettyPrint = true,
                CoordinatePrecision = 4
            });

            VoxelJsonParseResult reparsed = parser.Parse(exportedJson);
            Assert.False(reparsed.HasErrors);
            Assert.Equal(dialect, reparsed.DetectedDialect);
            Assert.Equal(source.Document.Voxels.Count, reparsed.Document.Voxels.Count);
        }
    }

    [Fact]
    public async Task Detector_CorrectlyIdentifiesVoxelJsonAndRejectsBedrockFiles()
    {
        var detector = new VoxelJsonFormatDetector();

        await using var voxelStream = File.OpenRead(GetFixturePath("voxel_nexus_v1.json"));
        var voxelDetect = await detector.DetectAsync(voxelStream, ".json");
        Assert.True(voxelDetect.IsRecognized);

        await using var bedrockStream = File.OpenRead(GetFixturePath("bedrock_1_16_0_guardian.geo.json"));
        var bedrockDetect = await detector.DetectAsync(bedrockStream, ".geo.json");
        Assert.False(bedrockDetect.IsRecognized);
    }
}

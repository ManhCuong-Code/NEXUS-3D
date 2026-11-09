using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Rules;
using NEXUS3D.Domain.ValueObjects;
using NEXUS3D.Shared.Helpers;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Domain.Tests;

public sealed class DomainFoundationTests
{
    [Theory]
    [InlineData("castle.schem", ModelFormatType.Schematic)]
    [InlineData("lobby.schematic", ModelFormatType.Schematic)]
    [InlineData("build.litematic", ModelFormatType.Schematic)]
    [InlineData("dragon.obj", ModelFormatType.Obj)]
    [InlineData("sword.json", ModelFormatType.VoxelJson)]
    [InlineData("chair.voxel.json", ModelFormatType.VoxelJson)]
    [InlineData("golem.geo.json", ModelFormatType.BedrockGeometry)]
    [InlineData("unknown.fbx", ModelFormatType.Unknown)]
    [InlineData("", ModelFormatType.Unknown)]
    public void DetectFormatFromPath_ShouldReturnExpectedFormat(string path, ModelFormatType expected)
    {
        ModelFormatType actual = ConversionDomainRules.DetectFormatFromPath(path);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValidateProfile_WithValidParameters_ShouldSucceed()
    {
        var profile = new ConversionProfile(
            name: "Standard Schematic to OBJ",
            description: "Test profile",
            sourceFormat: ModelFormatType.Schematic,
            targetFormat: ModelFormatType.Obj,
            scaleFactor: 1.0);

        OperationResult result = ConversionDomainRules.ValidateProfile(profile);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateProfile_WithInvalidScale_ShouldFail()
    {
        var profile = new ConversionProfile
        {
            Name = "Invalid Scale Profile",
            SourceFormat = ModelFormatType.Obj,
            TargetFormat = ModelFormatType.Schematic,
            ScaleFactor = -5.0
        };

        OperationResult result = ConversionDomainRules.ValidateProfile(profile);
        Assert.True(result.IsFailure);
        Assert.Equal("DOM_SCALE_RANGE", result.ErrorCode);
    }

    [Fact]
    public void BoundingBox3D_ShouldCalculateDimensionsAndVolumeCorrectly()
    {
        var box = new BoundingBox3D(
            new Vector3DValue(0, 0, 0),
            new Vector3DValue(16, 32, 8));

        Assert.Equal(16, box.Width);
        Assert.Equal(32, box.Height);
        Assert.Equal(8, box.Depth);
        Assert.Equal(4096, box.Volume);
    }

    [Fact]
    public void ConversionHistoryRecord_ShouldTransitionStatusCorrectly()
    {
        var record = new ConversionHistoryRecord(
            jobName: "Tower Conversion",
            sourceFilePath: "tower.schem",
            targetFilePath: "tower.obj",
            sourceFormat: ModelFormatType.Schematic,
            targetFormat: ModelFormatType.Obj,
            sourceSizeBytes: 2048);

        Assert.Equal(ConversionStatus.Pending, record.Status);

        record.MarkCompleted(outputSizeBytes: 8192, durationMs: 145);

        Assert.Equal(ConversionStatus.Completed, record.Status);
        Assert.Equal(8192, record.OutputSizeBytes);
        Assert.Equal(145, record.DurationMs);
        Assert.NotNull(record.CompletedAtUtc);
    }

    [Fact]
    public void FileSizeFormatter_ShouldFormatBytesCleanly()
    {
        Assert.Equal("0 B", FileSizeFormatter.FormatBytes(0));
        Assert.Equal("512 B", FileSizeFormatter.FormatBytes(512));
        Assert.Equal("2 KB", FileSizeFormatter.FormatBytes(2048));
    }
}


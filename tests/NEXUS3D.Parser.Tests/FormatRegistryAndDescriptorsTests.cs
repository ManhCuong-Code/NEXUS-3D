using NEXUS3D.Domain.Enums;
using NEXUS3D.Infrastructure.Formats;

namespace NEXUS3D.Parser.Tests;

public sealed class FormatRegistryAndDescriptorsTests
{
    private readonly FormatRegistry _registry = new();

    [Fact]
    public void GetAllFormats_ShouldContainAllFourCoreFormats()
    {
        var formats = _registry.GetAllFormats();

        Assert.Equal(4, formats.Count);
        Assert.Contains(formats, f => f.FormatType == ModelFormatType.Schematic);
        Assert.Contains(formats, f => f.FormatType == ModelFormatType.Obj);
        Assert.Contains(formats, f => f.FormatType == ModelFormatType.VoxelJson);
        Assert.Contains(formats, f => f.FormatType == ModelFormatType.BedrockGeometry);
    }

    [Theory]
    [InlineData("model.geo.json", ModelFormatType.BedrockGeometry)]
    [InlineData("block.json", ModelFormatType.VoxelJson)]
    [InlineData("item.voxel.json", ModelFormatType.VoxelJson)]
    [InlineData("mesh.obj", ModelFormatType.Obj)]
    [InlineData("spawn.schem", ModelFormatType.Schematic)]
    [InlineData("arena.litematic", ModelFormatType.Schematic)]
    public void ResolveByFilePath_ShouldPrioritizeCompoundExtensionsCorrectly(
        string filePath,
        ModelFormatType expectedType)
    {
        var descriptor = _registry.ResolveByFilePath(filePath);

        Assert.NotNull(descriptor);
        Assert.Equal(expectedType, descriptor.FormatType);
    }

    [Fact]
    public void BuildFileDialogFilter_ShouldIncludeAllFormatsAndWildcard()
    {
        string filter = _registry.BuildFileDialogFilter(includeAllSupported: true);

        Assert.Contains("All Supported 3D Models|", filter);
        Assert.Contains("*.schem", filter);
        Assert.Contains("*.obj", filter);
        Assert.Contains("*.geo.json", filter);
        Assert.Contains("All Files (*.*)|*.*", filter);
    }
}


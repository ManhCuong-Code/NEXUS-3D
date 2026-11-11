using System.Numerics;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class BlockMaterialRegistryTests
{
    [Theory]
    [InlineData("minecraft:stone", 0.48f, 0.48f, 0.48f, false)]
    [InlineData("minecraft:dirt", 0.52f, 0.37f, 0.25f, false)]
    [InlineData("minecraft:glass", 0.75f, 0.88f, 0.95f, true)]
    [InlineData("minecraft:water", 0.17f, 0.38f, 0.86f, true)]
    [InlineData("minecraft:diamond_block", 0.35f, 0.92f, 0.89f, false)]
    [InlineData("minecraft:gold_block", 0.96f, 0.81f, 0.21f, false)]
    public void GetBlockInfo_StandardBlocks_ReturnsAccurateColorAndProperties(
        string blockId, float expR, float expG, float expB, bool expTransparent)
    {
        var info = BlockMaterialRegistry.GetBlockInfo(blockId);

        Assert.NotNull(info);
        Assert.Equal(expTransparent, info.IsTransparent);
        Assert.InRange(info.Color.X, expR - 0.05f, expR + 0.05f);
        Assert.InRange(info.Color.Y, expG - 0.05f, expG + 0.05f);
        Assert.InRange(info.Color.Z, expB - 0.05f, expB + 0.05f);
    }

    [Fact]
    public void GetBlockInfo_SemanticKeywordFallback_IdentifiesModdedLeaves()
    {
        var info = BlockMaterialRegistry.GetBlockInfo("biomesoplenty:fir_leaves");

        Assert.NotNull(info);
        // Should fall back to vegetation green
        Assert.InRange(info.Color.X, 0.2f, 0.35f);
        Assert.InRange(info.Color.Y, 0.4f, 0.55f);
        Assert.True(info.Roughness > 0.8f);
    }

    [Fact]
    public void GetBlockInfo_UnknownBlock_ReturnsDeterministicHashColor()
    {
        var info1 = BlockMaterialRegistry.GetBlockInfo("unknown_mod:some_strange_block_123");
        var info2 = BlockMaterialRegistry.GetBlockInfo("unknown_mod:some_strange_block_123");

        Assert.NotNull(info1);
        Assert.Equal(info1.Color, info2.Color);
        Assert.Equal(1.0f, info1.Color.W);
    }
}

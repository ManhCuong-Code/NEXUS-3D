using System.IO;
using System.Numerics;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class TextureLoaderTests
{
    private readonly TextureLoader _loader = new();

    [Fact]
    public void ResolveTexturePath_RejectsPathTraversal()
    {
        var result = _loader.ResolveTexturePath("../../etc/passwd", "C:/Workspace");
        Assert.Null(result);

        var result2 = _loader.ResolveTexturePath("..\\..\\Windows\\System32\\cmd.exe", "C:/Workspace");
        Assert.Null(result2);
    }

    [Fact]
    public void ResolveTexturePath_HandlesRelativeAndAbsolutePaths()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var resolved = _loader.ResolveTexturePath(tempFile);
            Assert.NotNull(resolved);
            Assert.True(File.Exists(resolved));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void AnalyzeUVBounds_DetectsCorrectRangesAndTiling()
    {
        var uvs = new[]
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(0.5f, 0.8f),
            new Vector2(2.5f, 1.2f)
        };

        var (requiresTiling, minU, maxU, minV, maxV) = _loader.AnalyzeUVBounds(uvs);

        Assert.Equal(0.0f, minU);
        Assert.Equal(0.0f, minV);
        Assert.Equal(2.5f, maxU);
        Assert.Equal(1.2f, maxV);
        Assert.True(requiresTiling);
    }
}

using System.Numerics;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class MaterialRenderAdapterTests
{
    private readonly TextureLoader _textureLoader = new();
    private readonly MaterialRenderAdapter _adapter;

    public MaterialRenderAdapterTests()
    {
        _adapter = new MaterialRenderAdapter(_textureLoader);
    }

    [Fact]
    public void AdaptMaterial_TranslatesColorAndProperties()
    {
        var mat = new MaterialData("CustomMat")
        {
            BaseColor = new MaterialColor(0.2, 0.4, 0.8, 0.9),
            Roughness = 0.3,
            Metallic = 0.7,
            Opacity = 0.9
        };

        var renderMat = _adapter.AdaptMaterial(mat);

        Assert.Equal("CustomMat", renderMat.Name);
        Assert.Equal(0.2f, renderMat.BaseColor.X, 2);
        Assert.Equal(0.4f, renderMat.BaseColor.Y, 2);
        Assert.Equal(0.8f, renderMat.BaseColor.Z, 2);
        Assert.Equal(0.9f, renderMat.Opacity, 2);
    }

    [Fact]
    public void GetDefaultMaterial_ReturnsValidWhiteMaterial()
    {
        var defaultMat = _adapter.GetDefaultMaterial();

        Assert.NotNull(defaultMat);
        Assert.Equal("Default Material", defaultMat.Name);
        Assert.Equal(1.0f, defaultMat.Opacity);
    }

    [Fact]
    public void CreatePreviewSphere_GeneratesValidSphereMesh()
    {
        var sphere = _adapter.CreatePreviewSphere(_adapter.GetDefaultMaterial(), segments: 16, radius: 2.0f);

        Assert.NotNull(sphere);
        Assert.NotEmpty(sphere.Positions);
        Assert.NotEmpty(sphere.Normals);
        Assert.NotEmpty(sphere.Indices);
        Assert.Equal(sphere.Positions.Length, sphere.Normals.Length);
        Assert.Equal(sphere.Positions.Length, sphere.UVs.Length);
    }
}

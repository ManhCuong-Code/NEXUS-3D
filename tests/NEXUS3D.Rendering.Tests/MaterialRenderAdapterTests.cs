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

    [Fact]
    public void AdaptMaterial_FindsCompanionSkinTexture_BySkinPng()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "nexus3d_mat_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string skinFile = Path.Combine(tempDir, "skin.png");
            File.WriteAllBytes(skinFile, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }); // PNG header

            var mat = new MaterialData("geometry.humanoid.custom_material");
            var renderMat = _adapter.AdaptMaterial(mat, tempDir);

            Assert.NotNull(renderMat.DiffuseTexturePath);
            Assert.Equal(skinFile, renderMat.DiffuseTexturePath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void AdaptMaterial_FindsCompanionSkinTexture_InTexturesEntityDir()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "nexus3d_mat_test_" + Guid.NewGuid().ToString("N"));
        string entityDir = Path.Combine(tempDir, "textures", "entity");
        Directory.CreateDirectory(entityDir);
        try
        {
            string texFile = Path.Combine(entityDir, "steve.png");
            File.WriteAllBytes(texFile, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            var mat = new MaterialData("geometry.steve_material");
            var renderMat = _adapter.AdaptMaterial(mat, tempDir);

            Assert.NotNull(renderMat.DiffuseTexturePath);
            Assert.Equal(texFile, renderMat.DiffuseTexturePath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void AdaptMaterial_FindsSinglePng_WhenUnambiguous()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "nexus3d_mat_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string randomTex = Path.Combine(tempDir, "character_model_diffuse_512.png");
            File.WriteAllBytes(randomTex, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            var mat = new MaterialData("geometry.unknown_material");
            var renderMat = _adapter.AdaptMaterial(mat, tempDir);

            Assert.NotNull(renderMat.DiffuseTexturePath);
            Assert.Equal(randomTex, renderMat.DiffuseTexturePath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}

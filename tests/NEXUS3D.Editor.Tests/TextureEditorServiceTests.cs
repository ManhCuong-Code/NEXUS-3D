using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Editor.Engine.Services;
using Xunit;

namespace NEXUS3D.Editor.Tests;

public sealed class TextureEditorServiceTests
{
    [Fact]
    public void AssignTexture_CreatesSlotOrUpdatesId()
    {
        var service = new TextureEditorService();
        var mat = new MaterialData("TestMat");
        var texId1 = Guid.NewGuid();
        var texId2 = Guid.NewGuid();

        service.AssignTexture(mat, texId1, "Albedo");
        Assert.True(mat.TextureSlots.ContainsKey("Albedo"));
        Assert.Equal(texId1, mat.TextureSlots["Albedo"].TextureId);

        // Replace
        service.AssignTexture(mat, texId2, "Albedo");
        Assert.Equal(texId2, mat.TextureSlots["Albedo"].TextureId);

        // Remove
        service.RemoveTexture(mat, "Albedo");
        Assert.False(mat.TextureSlots.ContainsKey("Albedo"));
    }

    [Fact]
    public void TransformUVs_ScalesAndOffsetsCorrectly()
    {
        var service = new TextureEditorService();
        var mesh = new MeshData("Mesh");
        mesh.Vertices.Add(new Vertex(Vector3D.Zero, null, new Vector2D(0.5, 0.5)));

        // Offset (0.1, 0.2), scale (2, 2), rotation 0
        service.TransformUVs(mesh, new Vector2D(0.1, 0.2), new Vector2D(2, 2), rotationDegrees: 0);

        var uv = mesh.Vertices[0].TexCoord;
        Assert.NotNull(uv);
        // (0.5 - 0.5)*2 + 0.5)*2? Wait, relative to (0.5, 0.5): (0.5-0.5) = 0. uRot = 0.5. uFinal = 0.5 * 2 + 0.1 = 1.1
        Assert.True(Math.Abs(uv.Value.X - 1.1) < 1e-4);
        Assert.True(Math.Abs(uv.Value.Y - 1.2) < 1e-4);
    }

    [Fact]
    public void FlipUVs_FlipsHorizontallyAndVertically()
    {
        var service = new TextureEditorService();
        var mesh = new MeshData("Mesh");
        mesh.Vertices.Add(new Vertex(Vector3D.Zero, null, new Vector2D(0.2, 0.7)));

        service.FlipUVs(mesh, horizontal: true, vertical: true);

        var uv = mesh.Vertices[0].TexCoord;
        Assert.NotNull(uv);
        Assert.True(Math.Abs(uv.Value.X - 0.8) < 1e-4);
        Assert.True(Math.Abs(uv.Value.Y - 0.3) < 1e-4);
    }
}


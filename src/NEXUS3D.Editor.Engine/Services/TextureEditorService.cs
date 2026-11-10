using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.Editor.Engine.Services;

/// <summary>
/// Service managing texture assignment to material slots and UV coordinate transformations on meshes.
/// </summary>
public sealed class TextureEditorService : ITextureEditorService
{
    public void AssignTexture(MaterialData material, Guid textureId, string slotName = "Albedo")
    {
        ArgumentNullException.ThrowIfNull(material);
        DomainGuard.NotNullOrWhiteSpace(slotName);

        if (material.TextureSlots.TryGetValue(slotName, out var existingSlot))
        {
            existingSlot.TextureId = textureId;
        }
        else
        {
            material.TextureSlots[slotName] = new TextureSlot(textureId, slotName)
            {
                Offset = Vector2D.Zero,
                Scale = Vector2D.One,
                Rotation = 0.0
            };
        }
    }

    public void RemoveTexture(MaterialData material, string slotName = "Albedo")
    {
        ArgumentNullException.ThrowIfNull(material);
        DomainGuard.NotNullOrWhiteSpace(slotName);

        material.TextureSlots.Remove(slotName);
    }

    public void SetTextureTransform(
        MaterialData material,
        string slotName,
        Vector2D offset,
        Vector2D scale,
        double rotationDegrees)
    {
        ArgumentNullException.ThrowIfNull(material);
        DomainGuard.NotNullOrWhiteSpace(slotName);

        if (material.TextureSlots.TryGetValue(slotName, out var slot))
        {
            slot.Offset = offset;
            slot.Scale = scale;
            slot.Rotation = rotationDegrees;
        }
    }

    public void TransformUVs(MeshData mesh, Vector2D offset, Vector2D scale, double rotationDegrees)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        double rad = MathUtilities.DegreesToRadians(rotationDegrees);
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);

        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            var v = mesh.Vertices[i];
            if (!v.TexCoord.HasValue) continue;

            var tc = v.TexCoord.Value;

            // Rotate around center (0.5, 0.5)
            double uRel = tc.X - 0.5;
            double vRel = tc.Y - 0.5;

            double uRot = uRel * cos - vRel * sin + 0.5;
            double vRot = uRel * sin + vRel * cos + 0.5;

            // Scale and offset
            double uFinal = uRot * scale.X + offset.X;
            double vFinal = vRot * scale.Y + offset.Y;

            v.TexCoord = new Vector2D(uFinal, vFinal);
        }
    }

    public void FlipUVs(MeshData mesh, bool horizontal, bool vertical)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!horizontal && !vertical) return;

        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            var v = mesh.Vertices[i];
            if (!v.TexCoord.HasValue) continue;

            var tc = v.TexCoord.Value;
            double u = horizontal ? 1.0 - tc.X : tc.X;
            double y = vertical ? 1.0 - tc.Y : tc.Y;

            v.TexCoord = new Vector2D(u, y);
        }
    }
}

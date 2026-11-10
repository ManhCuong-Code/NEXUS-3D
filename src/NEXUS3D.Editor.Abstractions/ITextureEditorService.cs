using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;

namespace NEXUS3D.Editor.Abstractions;

/// <summary>
/// Service managing texture slot assignments and interactive UV mapping manipulations.
/// </summary>
public interface ITextureEditorService
{
    /// <summary>
    /// Assigns a texture by ID to a named slot on a material (e.g. "Albedo", "Diffuse", "Normal").
    /// </summary>
    void AssignTexture(MaterialData material, Guid textureId, string slotName = "Albedo");

    /// <summary>
    /// Removes the texture binding from a named slot on a material.
    /// </summary>
    void RemoveTexture(MaterialData material, string slotName = "Albedo");

    /// <summary>
    /// Updates the UV offset, scale/tiling, and rotation for a material texture slot.
    /// </summary>
    void SetTextureTransform(MaterialData material, string slotName, Vector2D offset, Vector2D scale, double rotationDegrees);

    /// <summary>
    /// Directly applies UV coordinate transformations (translation offset, scaling, rotation) to vertex texture coordinates in a mesh.
    /// </summary>
    void TransformUVs(MeshData mesh, Vector2D offset, Vector2D scale, double rotationDegrees);

    /// <summary>
    /// Flips vertex UV coordinates horizontally (U -> 1 - U) or vertically (V -> 1 - V).
    /// </summary>
    void FlipUVs(MeshData mesh, bool horizontal, bool vertical);
}


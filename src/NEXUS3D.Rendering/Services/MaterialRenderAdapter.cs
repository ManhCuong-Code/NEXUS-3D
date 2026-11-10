using System.Numerics;
using System.Text.Json;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Textures;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Adapts format-independent USM MaterialData and Texture references into GPU RenderMaterial.
/// </summary>
public sealed class MaterialRenderAdapter : IMaterialRenderAdapter
{
    private readonly ITextureLoader _textureLoader;

    public MaterialRenderAdapter(ITextureLoader textureLoader)
    {
        _textureLoader = textureLoader;
    }

    public RenderMaterial GetDefaultMaterial() => RenderMaterial.Default;

    public RenderMaterial AdaptMaterial(MaterialData material, string? baseDirectory = null)
    {
        if (material is null)
        {
            return GetDefaultMaterial();
        }

        var baseColor = new Vector4(
            (float)material.BaseColor.R,
            (float)material.BaseColor.G,
            (float)material.BaseColor.B,
            (float)(material.BaseColor.A * material.Opacity));

        var spec = Vector4.One * 0.2f;
        float shininess = 20f;
        bool isDoubleSided = true;

        if (material.CustomProperties.TryGetValue("shininess", out var shineElem) && shineElem.ValueKind == JsonValueKind.Number)
        {
            shininess = (float)shineElem.GetDouble();
        }

        if (material.CustomProperties.TryGetValue("isDoubleSided", out var sideElem) && sideElem.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            isDoubleSided = sideElem.GetBoolean();
        }

        string? diffusePath = null;
        string? normalPath = null;
        string? emissivePath = null;

        // Check Diffuse / Albedo slots
        if (material.TextureSlots.TryGetValue("Albedo", out var albedoslot) ||
            material.TextureSlots.TryGetValue("Diffuse", out albedoslot) ||
            material.TextureSlots.TryGetValue("map_Kd", out albedoslot))
        {
            diffusePath = albedoslot.Channel;
        }

        if (material.TextureSlots.TryGetValue("Normal", out var normSlot) ||
            material.TextureSlots.TryGetValue("Bump", out normSlot) ||
            material.TextureSlots.TryGetValue("map_Bump", out normSlot))
        {
            normalPath = normSlot.Channel;
        }

        if (material.TextureSlots.TryGetValue("Emissive", out var emSlot))
        {
            emissivePath = emSlot.Channel;
        }

        // Also check CustomProperties for texture paths
        if (string.IsNullOrEmpty(diffusePath) && material.CustomProperties.TryGetValue("diffuseTexture", out var dtElem))
        {
            diffusePath = dtElem.GetString();
        }

        // Resolve paths
        diffusePath = _textureLoader.ResolveTexturePath(diffusePath ?? string.Empty, baseDirectory);
        normalPath = _textureLoader.ResolveTexturePath(normalPath ?? string.Empty, baseDirectory);
        emissivePath = _textureLoader.ResolveTexturePath(emissivePath ?? string.Empty, baseDirectory);

        return new RenderMaterial(
            Id: material.Id.ToString(),
            Name: material.Name,
            BaseColor: baseColor,
            SpecularColor: spec,
            Shininess: shininess,
            Opacity: (float)material.Opacity,
            IsDoubleSided: isDoubleSided,
            DiffuseTexturePath: diffusePath,
            NormalTexturePath: normalPath,
            EmissiveTexturePath: emissivePath);
    }
}

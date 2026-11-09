using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Textures;

namespace NEXUS3D.Domain.Validation;

/// <summary>
/// Validates <see cref="MaterialData"/> and <see cref="TextureData"/> properties and ranges.
/// </summary>
public sealed class MaterialValidator : IValidator<MaterialData>
{
    public ValidationResult Validate(MaterialData? material)
    {
        var result = new ValidationResult();
        if (material is null)
        {
            result.AddError(UsmErrorCodes.MaterialReferenceNotFound, "MaterialData cannot be null.", "Material");
            return result;
        }

        if (material.Id == Guid.Empty)
        {
            result.AddError(UsmErrorCodes.InvalidScene, "Material ID cannot be Guid.Empty.", "Material.Id");
        }

        if (string.IsNullOrWhiteSpace(material.Name))
        {
            result.AddError(UsmErrorCodes.InvalidScene, "Material name cannot be empty.", "Material.Name", material.Id);
        }

        if (!material.BaseColor.IsValid)
        {
            result.AddError(
                UsmErrorCodes.InvalidScene,
                $"Material '{material.Name}' BaseColor components must be in range [0, 1].",
                "Material.BaseColor",
                material.Id);
        }

        if (!double.IsFinite(material.Metallic) || material.Metallic < 0.0 || material.Metallic > 1.0)
        {
            result.AddError(
                UsmErrorCodes.InvalidScene,
                $"Material '{material.Name}' Metallic ({material.Metallic}) must be between 0 and 1.",
                "Material.Metallic",
                material.Id);
        }

        if (!double.IsFinite(material.Roughness) || material.Roughness < 0.0 || material.Roughness > 1.0)
        {
            result.AddError(
                UsmErrorCodes.InvalidScene,
                $"Material '{material.Name}' Roughness ({material.Roughness}) must be between 0 and 1.",
                "Material.Roughness",
                material.Id);
        }

        if (!double.IsFinite(material.Opacity) || material.Opacity < 0.0 || material.Opacity > 1.0)
        {
            result.AddError(
                UsmErrorCodes.InvalidScene,
                $"Material '{material.Name}' Opacity ({material.Opacity}) must be between 0 and 1.",
                "Material.Opacity",
                material.Id);
        }

        foreach (var kvp in material.TextureSlots)
        {
            TextureSlot slot = kvp.Value;
            if (slot.TextureId == Guid.Empty)
            {
                result.AddError(
                    UsmErrorCodes.TextureReferenceNotFound,
                    $"Material '{material.Name}' slot '{kvp.Key}' has an empty TextureId.",
                    $"Material.TextureSlots[{kvp.Key}].TextureId",
                    material.Id);
            }

            if (!slot.Offset.IsFinite() || !slot.Scale.IsFinite() || !double.IsFinite(slot.Rotation))
            {
                result.AddError(
                    UsmErrorCodes.InvalidTransform,
                    $"Material '{material.Name}' slot '{kvp.Key}' has non-finite UV transform parameters.",
                    $"Material.TextureSlots[{kvp.Key}]",
                    material.Id);
            }
        }

        return result;
    }

    public ValidationResult ValidateTexture(TextureData? texture)
    {
        var result = new ValidationResult();
        if (texture is null)
        {
            result.AddError(UsmErrorCodes.TextureReferenceNotFound, "TextureData cannot be null.", "Texture");
            return result;
        }

        if (texture.Id == Guid.Empty)
        {
            result.AddError(UsmErrorCodes.TextureReferenceNotFound, "Texture ID cannot be Guid.Empty.", "Texture.Id");
        }

        if (string.IsNullOrWhiteSpace(texture.Name))
        {
            result.AddError(UsmErrorCodes.TextureReferenceNotFound, "Texture name cannot be empty.", "Texture.Name", texture.Id);
        }

        if (texture.Width <= 0 || texture.Height <= 0)
        {
            result.AddError(
                UsmErrorCodes.TextureReferenceNotFound,
                $"Texture '{texture.Name}' dimensions ({texture.Width}x{texture.Height}) must be positive.",
                "Texture.Size",
                texture.Id);
        }

        if (texture.Reference is null || !texture.Reference.IsValid)
        {
            result.AddError(
                UsmErrorCodes.TextureReferenceNotFound,
                $"Texture '{texture.Name}' has a missing or invalid TextureReference.",
                "Texture.Reference",
                texture.Id);
        }

        return result;
    }
}


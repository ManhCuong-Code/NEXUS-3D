using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Domain.Rules;

/// <summary>
/// Core domain rules governing format identification and conversion profile validation.
/// Pure domain logic with zero infrastructure dependencies.
/// </summary>
public static class ConversionDomainRules
{
    public const double MinScaleFactor = 0.0001;
    public const double MaxScaleFactor = 10000.0;

    /// <summary>
    /// Resolves the <see cref="ModelFormatType"/> from a file name or extension.
    /// </summary>
    public static ModelFormatType DetectFormatFromPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ModelFormatType.Unknown;
        }

        string lower = filePath.Trim().ToLowerInvariant();

        if (lower.EndsWith(".geo.json", StringComparison.Ordinal))
        {
            return ModelFormatType.BedrockGeometry;
        }

        if (lower.EndsWith(".schem", StringComparison.Ordinal) ||
            lower.EndsWith(".schematic", StringComparison.Ordinal) ||
            lower.EndsWith(".litematic", StringComparison.Ordinal))
        {
            return ModelFormatType.Schematic;
        }

        if (lower.EndsWith(".obj", StringComparison.Ordinal))
        {
            return ModelFormatType.Obj;
        }

        if (lower.EndsWith(".json", StringComparison.Ordinal) ||
            lower.EndsWith(".voxel.json", StringComparison.Ordinal))
        {
            return ModelFormatType.VoxelJson;
        }

        return ModelFormatType.Unknown;
    }

    /// <summary>
    /// Validates whether a conversion profile meets all domain invariants.
    /// </summary>
    public static OperationResult ValidateProfile(ConversionProfile? profile)
    {
        if (profile is null)
        {
            return OperationResult.Failure("DOM_PROFILE_NULL", "Conversion profile cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            return OperationResult.Failure("DOM_PROFILE_NAME", "Profile name is required.");
        }

        if (profile.SourceFormat == ModelFormatType.Unknown)
        {
            return OperationResult.Failure("DOM_SOURCE_FORMAT", "Source format must be specified.");
        }

        if (profile.TargetFormat == ModelFormatType.Unknown)
        {
            return OperationResult.Failure("DOM_TARGET_FORMAT", "Target format must be specified.");
        }

        if (double.IsNaN(profile.ScaleFactor) ||
            profile.ScaleFactor < MinScaleFactor ||
            profile.ScaleFactor > MaxScaleFactor)
        {
            return OperationResult.Failure(
                "DOM_SCALE_RANGE",
                $"Scale factor must be between {MinScaleFactor} and {MaxScaleFactor}.");
        }

        return OperationResult.Success();
    }
}


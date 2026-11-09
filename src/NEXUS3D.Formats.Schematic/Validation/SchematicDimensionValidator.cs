using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Validation;

/// <summary>
/// Validates schematic dimensions (Width, Height, Length) and total block count against configured limits.
/// </summary>
public sealed class SchematicDimensionValidator
{
    private readonly int _maxDimensionSize;
    private readonly int _maxTotalBlocks;

    public SchematicDimensionValidator(int maxDimensionSize = 4096, int maxTotalBlocks = 67_108_864)
    {
        _maxDimensionSize = maxDimensionSize;
        _maxTotalBlocks = maxTotalBlocks;
    }

    public ValidationResult Validate(SchematicDimensions dimensions)
    {
        var result = new ValidationResult();

        if (!dimensions.IsValid)
        {
            result.AddError(
                SchematicErrorCodes.InvalidDimensions,
                $"Schematic dimensions ({dimensions.Width}x{dimensions.Height}x{dimensions.Length}) must all be positive.",
                "Dimensions");
            return result;
        }

        if (dimensions.Width > _maxDimensionSize ||
            dimensions.Height > _maxDimensionSize ||
            dimensions.Length > _maxDimensionSize)
        {
            result.AddError(
                SchematicErrorCodes.LimitExceeded,
                $"Schematic dimension ({dimensions.Width}x{dimensions.Height}x{dimensions.Length}) exceeds MaxDimensionSize ({_maxDimensionSize}).",
                "Dimensions");
        }

        if (dimensions.VolumeLong > _maxTotalBlocks)
        {
            result.AddError(
                SchematicErrorCodes.LimitExceeded,
                $"Schematic volume ({dimensions.VolumeLong}) exceeds MaxTotalBlocks ({_maxTotalBlocks}).",
                "Dimensions.Volume");
        }

        return result;
    }
}


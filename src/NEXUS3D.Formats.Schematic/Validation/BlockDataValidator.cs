using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Palette;

namespace NEXUS3D.Formats.Schematic.Validation;

/// <summary>
/// Validates decoded linear block indices and palette entries in a <see cref="SchematicDocument"/>.
/// </summary>
public sealed class BlockDataValidator
{
    public ValidationResult Validate(SchematicPalette palette, int[] blockIndices, SchematicDimensions dimensions)
    {
        var result = new ValidationResult();

        if (palette is null || palette.Count == 0)
        {
            result.AddError(SchematicErrorCodes.InvalidPalette, "Schematic palette cannot be empty.", "Palette");
            return result;
        }

        long expectedCount = dimensions.VolumeLong;
        if (blockIndices is null || blockIndices.Length != expectedCount)
        {
            result.AddError(
                SchematicErrorCodes.InvalidBlockData,
                $"BlockIndices length ({blockIndices?.Length ?? 0}) does not match expected volume ({expectedCount}).",
                "BlockIndices");
            return result;
        }

        for (int i = 0; i < blockIndices.Length; i++)
        {
            int idx = blockIndices[i];
            if (idx < 0 || !palette.ContainsIndex(idx))
            {
                result.AddError(
                    SchematicErrorCodes.InvalidPalette,
                    $"Block at linear index {i} references missing palette index {idx}.",
                    $"BlockIndices[{i}]");
                break;
            }
        }

        return result;
    }
}

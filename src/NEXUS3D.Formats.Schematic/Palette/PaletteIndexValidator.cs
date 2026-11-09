using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Palette;

/// <summary>
/// Validates decoded palette indices against a <see cref="SchematicPalette"/>.
/// </summary>
public static class PaletteIndexValidator
{
    public static void ValidatePaletteAndIndices(
        SchematicPalette palette,
        ReadOnlySpan<int> blockIndices,
        int expectedBlockCount,
        int? declaredPaletteMax = null)
    {
        ArgumentNullException.ThrowIfNull(palette);

        if (palette.Count == 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidPalette,
                "Schematic palette cannot be empty.");
        }

        if (declaredPaletteMax.HasValue && declaredPaletteMax.Value <= 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidPalette,
                $"Schematic PaletteMax ({declaredPaletteMax.Value}) must be positive.");
        }

        if (blockIndices.Length != expectedBlockCount)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockData,
                $"Decoded block count ({blockIndices.Length}) does not match expected volume ({expectedBlockCount}).");
        }

        for (int i = 0; i < blockIndices.Length; i++)
        {
            int idx = blockIndices[i];
            if (idx < 0 || !palette.ContainsIndex(idx))
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidPalette,
                    $"Block at linear index {i} references undefined palette index {idx}.");
            }
        }
    }
}

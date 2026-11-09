using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Legacy;

/// <summary>
/// Validates Legacy <c>.schematic</c> dimensions and required NBT fields.
/// </summary>
public static class LegacyFormatValidator
{
    public static SchematicDimensions ReadAndValidateDimensions(
        NbtCompoundTag compound,
        SchematicReadOptions options)
    {
        if (!compound.TryGetShort("Width", out short widthRaw) ||
            !compound.TryGetShort("Height", out short heightRaw) ||
            !compound.TryGetShort("Length", out short lengthRaw))
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Legacy schematic is missing required Width, Height, or Length TAG_Short fields.");
        }

        int width = widthRaw;
        int height = heightRaw;
        int length = lengthRaw;

        if (width <= 0 || height <= 0 || length <= 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidDimensions,
                $"Legacy schematic dimensions ({width}x{height}x{length}) must be greater than zero.");
        }

        if (width > options.MaxDimensionSize || height > options.MaxDimensionSize || length > options.MaxDimensionSize)
        {
            throw new SchematicException(
                SchematicErrorCodes.LimitExceeded,
                $"Legacy schematic dimensions ({width}x{height}x{length}) exceed MaxDimensionSize ({options.MaxDimensionSize}).");
        }

        var dims = new SchematicDimensions(width, height, length);
        _ = dims.GetCheckedVolume(options.MaxTotalBlocks);
        return dims;
    }
}

using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// Validates Sponge v2 and v3 NBT compounds prior to decoding.
/// </summary>
public static class SpongeFormatValidator
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
                "Sponge schematic is missing required Width, Height, or Length TAG_Short fields.");
        }

        // Sponge specification defines Width, Height, Length as unsigned 16-bit integers stored in TAG_Short
        int width = widthRaw < 0 ? (ushort)widthRaw : widthRaw;
        int height = heightRaw < 0 ? (ushort)heightRaw : heightRaw;
        int length = lengthRaw < 0 ? (ushort)lengthRaw : lengthRaw;

        if (width <= 0 || height <= 0 || length <= 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidDimensions,
                $"Sponge schematic dimensions ({width}x{height}x{length}) must be greater than zero.");
        }

        if (width > options.MaxDimensionSize || height > options.MaxDimensionSize || length > options.MaxDimensionSize)
        {
            throw new SchematicException(
                SchematicErrorCodes.LimitExceeded,
                $"Sponge schematic dimensions ({width}x{height}x{length}) exceed MaxDimensionSize ({options.MaxDimensionSize}).");
        }

        var dims = new SchematicDimensions(width, height, length);
        _ = dims.GetCheckedVolume(options.MaxTotalBlocks);
        return dims;
    }
}

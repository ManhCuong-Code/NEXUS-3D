using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Version metadata and standardized YZX linear indexing math for Sponge and Legacy schematics.
/// </summary>
public static class SchematicIndexing
{
    /// <summary>
    /// Computes the linear block index: <c>index = x + z * width + y * width * length</c>
    /// where X varies fastest, then Z, and finally Y.
    /// Uses checked arithmetic to prevent integer overflow.
    /// </summary>
    public static int ToIndex(int x, int y, int z, int width, int length)
    {
        if (width <= 0 || length <= 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidDimensions,
                $"Width ({width}) and Length ({length}) must be positive.");
        }

        if (x < 0 || x >= width || z < 0 || z >= length || y < 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidDimensions,
                $"Coordinates ({x}, {y}, {z}) are outside schematic bounds (Width={width}, Length={length}).");
        }

        try
        {
            return checked(x + z * width + y * width * length);
        }
        catch (OverflowException ex)
        {
            throw new SchematicException(
                SchematicErrorCodes.LimitExceeded,
                $"Block index overflow at ({x}, {y}, {z}) for Width={width}, Length={length}.",
                ex);
        }
    }

    /// <summary>
    /// Converts a linear YZX block index back into 3D <see cref="VoxelPosition"/> coordinates.
    /// </summary>
    public static VoxelPosition FromIndex(int index, int width, int height, int length)
    {
        if (width <= 0 || height <= 0 || length <= 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidDimensions,
                $"Invalid schematic dimensions ({width}x{height}x{length}).");
        }

        int layerSize = checked(width * length);
        int totalBlocks = checked(layerSize * height);

        if (index < 0 || index >= totalBlocks)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockData,
                $"Block index {index} is out of range [0, {totalBlocks - 1}].");
        }

        int y = index / layerSize;
        int remainder = index % layerSize;
        int z = remainder / width;
        int x = remainder % width;

        return new VoxelPosition(x, y, z);
    }
}


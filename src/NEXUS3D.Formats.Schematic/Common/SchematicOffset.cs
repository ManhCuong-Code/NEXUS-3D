using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Represents the 3D integer offset of a schematic relative to its origin or copy point.
/// </summary>
public readonly record struct SchematicOffset(int X, int Y, int Z)
{
    public static SchematicOffset Zero => new(0, 0, 0);

    public int[] ToIntArray() => [X, Y, Z];

    public VoxelPosition ToVoxelPosition() => new(X, Y, Z);

    public static SchematicOffset FromVoxelPosition(VoxelPosition pos) =>
        new(pos.X, pos.Y, pos.Z);

    public static SchematicOffset FromIntArray(int[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return Zero;
        }

        if (values.Length != 3)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidOffset,
                $"Schematic Offset IntArray must contain exactly 3 elements, but found {values.Length}.");
        }

        return new SchematicOffset(values[0], values[1], values[2]);
    }
}


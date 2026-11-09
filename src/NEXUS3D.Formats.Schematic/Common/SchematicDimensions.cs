using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Represents the Width (X), Height (Y), and Length (Z) dimensions of a schematic.
/// </summary>
public readonly record struct SchematicDimensions(int Width, int Height, int Length)
{
    public bool IsValid => Width > 0 && Height > 0 && Length > 0;

    public long VolumeLong => (long)Width * Height * Length;

    public int GetCheckedVolume(int maxAllowedBlocks = 67_108_864)
    {
        if (!IsValid)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidDimensions,
                $"Schematic dimensions ({Width}x{Height}x{Length}) must all be greater than zero.");
        }

        long vol = VolumeLong;
        if (vol > maxAllowedBlocks || vol > int.MaxValue)
        {
            throw new SchematicException(
                SchematicErrorCodes.LimitExceeded,
                $"Schematic volume ({vol} blocks) exceeds maximum allowed block count ({maxAllowedBlocks}).");
        }

        return (int)vol;
    }

    public bool Contains(int x, int y, int z) =>
        x >= 0 && x < Width &&
        y >= 0 && y < Height &&
        z >= 0 && z < Length;

    public bool Contains(VoxelPosition pos) =>
        Contains(pos.X, pos.Y, pos.Z);
}


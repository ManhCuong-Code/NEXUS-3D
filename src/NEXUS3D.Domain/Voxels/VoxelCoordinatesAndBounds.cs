using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Voxels;

/// <summary>
/// Discrete 3D integer coordinate of a voxel cell.
/// </summary>
public readonly record struct VoxelPosition(int X, int Y, int Z) : IComparable<VoxelPosition>
{
    public static VoxelPosition Zero => new(0, 0, 0);
    public static VoxelPosition One => new(1, 1, 1);

    public Vector3D ToVector3D() => new(X, Y, Z);

    public Vector3D ToWorldPosition(VoxelSize voxelSize) =>
        new(X * voxelSize.X, Y * voxelSize.Y, Z * voxelSize.Z);

    public long ManhattanDistance(VoxelPosition other) =>
        Math.Abs((long)X - other.X) +
        Math.Abs((long)Y - other.Y) +
        Math.Abs((long)Z - other.Z);

    public double EuclideanDistance(VoxelPosition other)
    {
        double dx = (double)X - other.X;
        double dy = (double)Y - other.Y;
        double dz = (double)Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public int CompareTo(VoxelPosition other)
    {
        int yCmp = Y.CompareTo(other.Y);
        if (yCmp != 0) return yCmp;
        int zCmp = Z.CompareTo(other.Z);
        if (zCmp != 0) return zCmp;
        return X.CompareTo(other.X);
    }

    public static VoxelPosition Min(VoxelPosition a, VoxelPosition b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));

    public static VoxelPosition Max(VoxelPosition a, VoxelPosition b) =>
        new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));

    public static VoxelPosition operator +(VoxelPosition a, VoxelPosition b) =>
        new(checked(a.X + b.X), checked(a.Y + b.Y), checked(a.Z + b.Z));

    public static VoxelPosition operator -(VoxelPosition a, VoxelPosition b) =>
        new(checked(a.X - b.X), checked(a.Y - b.Y), checked(a.Z - b.Z));
}

/// <summary>
/// Physical dimensions of a single voxel cell along X, Y, Z axes.
/// Allows both uniform cubic voxels and non-uniform rectangular voxels.
/// </summary>
public readonly record struct VoxelSize(double X, double Y, double Z)
{
    public static VoxelSize UnitCube => new(1.0, 1.0, 1.0);

    public bool IsValid =>
        double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z) &&
        X > 0.0 && Y > 0.0 && Z > 0.0;

    public bool IsCubic =>
        IsValid &&
        MathUtilities.ApproximatelyEqual(X, Y) &&
        MathUtilities.ApproximatelyEqual(Y, Z);

    public Vector3D ToVector3D() => new(X, Y, Z);
}

/// <summary>
/// Inclusive integer bounding range of a <see cref="VoxelGrid"/>.
/// </summary>
public readonly record struct VoxelGridBounds(VoxelPosition Min, VoxelPosition Max)
{
    public static VoxelGridBounds Empty => new(
        new VoxelPosition(1, 1, 1),
        new VoxelPosition(0, 0, 0));

    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    public bool IsValid => !IsEmpty;

    public long Width => IsValid ? (long)Max.X - Min.X + 1 : 0;
    public long Height => IsValid ? (long)Max.Y - Min.Y + 1 : 0;
    public long Depth => IsValid ? (long)Max.Z - Min.Z + 1 : 0;

    public bool Contains(VoxelPosition position) =>
        IsValid &&
        position.X >= Min.X && position.X <= Max.X &&
        position.Y >= Min.Y && position.Y <= Max.Y &&
        position.Z >= Min.Z && position.Z <= Max.Z;

    public VoxelGridBounds Encapsulate(VoxelPosition position) =>
        IsEmpty
            ? new VoxelGridBounds(position, position)
            : new VoxelGridBounds(VoxelPosition.Min(Min, position), VoxelPosition.Max(Max, position));
}


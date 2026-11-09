namespace NEXUS3D.Domain.Mathematics;

/// <summary>
/// Axis-aligned 3D bounding box (AABB) supporting empty state, zero-size bounds, containment,
/// intersection, encapsulation, and matrix transformation (including rotations).
/// </summary>
public readonly record struct BoundingBox(Vector3D Min, Vector3D Max)
{
    public static BoundingBox Empty => new(
        new Vector3D(double.MaxValue, double.MaxValue, double.MaxValue),
        new Vector3D(double.MinValue, double.MinValue, double.MinValue));

    public bool IsEmpty =>
        Min.X == double.MaxValue && Max.X == double.MinValue;

    public bool IsValid =>
        !IsEmpty &&
        Min.IsFinite() &&
        Max.IsFinite() &&
        Min.X <= Max.X &&
        Min.Y <= Max.Y &&
        Min.Z <= Max.Z;

    public Vector3D Center => IsValid
        ? (Min + Max) * 0.5
        : Vector3D.Zero;

    public Vector3D Size => IsValid
        ? Max - Min
        : Vector3D.Zero;

    public double Volume => IsValid
        ? Size.X * Size.Y * Size.Z
        : 0.0;

    public bool Contains(Vector3D point)
    {
        if (!IsValid || !point.IsFinite())
        {
            return false;
        }

        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y &&
               point.Z >= Min.Z && point.Z <= Max.Z;
    }

    public bool Intersects(BoundingBox other)
    {
        if (!IsValid || !other.IsValid)
        {
            return false;
        }

        return Min.X <= other.Max.X && Max.X >= other.Min.X &&
               Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
               Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }

    public BoundingBox Encapsulate(Vector3D point)
    {
        if (!point.IsFinite())
        {
            return this;
        }

        if (!IsValid)
        {
            return new BoundingBox(point, point);
        }

        return new BoundingBox(
            Vector3D.Min(Min, point),
            Vector3D.Max(Max, point));
    }

    public BoundingBox Encapsulate(BoundingBox other)
    {
        if (!other.IsValid)
        {
            return this;
        }

        if (!IsValid)
        {
            return other;
        }

        return new BoundingBox(
            Vector3D.Min(Min, other.Min),
            Vector3D.Max(Max, other.Max));
    }

    /// <summary>
    /// Transforms all 8 corners of the bounding box by the specified matrix and returns the enclosing AABB.
    /// </summary>
    public BoundingBox Transform(Matrix4x4D matrix)
    {
        if (!IsValid)
        {
            return Empty;
        }

        Span<Vector3D> corners =
        [
            new Vector3D(Min.X, Min.Y, Min.Z),
            new Vector3D(Max.X, Min.Y, Min.Z),
            new Vector3D(Min.X, Max.Y, Min.Z),
            new Vector3D(Max.X, Max.Y, Min.Z),
            new Vector3D(Min.X, Min.Y, Max.Z),
            new Vector3D(Max.X, Min.Y, Max.Z),
            new Vector3D(Min.X, Max.Y, Max.Z),
            new Vector3D(Max.X, Max.Y, Max.Z)
        ];

        Vector3D first = matrix.TransformPoint(corners[0]);
        Vector3D newMin = first;
        Vector3D newMax = first;

        for (int i = 1; i < corners.Length; i++)
        {
            Vector3D transformed = matrix.TransformPoint(corners[i]);
            newMin = Vector3D.Min(newMin, transformed);
            newMax = Vector3D.Max(newMax, transformed);
        }

        return new BoundingBox(newMin, newMax);
    }

    public static BoundingBox FromPoints(IEnumerable<Vector3D> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        BoundingBox result = Empty;
        foreach (var point in points)
        {
            result = result.Encapsulate(point);
        }

        return result;
    }
}


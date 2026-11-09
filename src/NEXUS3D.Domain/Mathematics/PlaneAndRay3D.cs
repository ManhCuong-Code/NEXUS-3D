namespace NEXUS3D.Domain.Mathematics;

/// <summary>
/// Mathematical 3D plane defined by a unit normal and signed distance from origin (Dot(Normal, P) + D = 0).
/// </summary>
public readonly record struct Plane3D(Vector3D Normal, double D)
{
    public static Plane3D FromPointAndNormal(Vector3D point, Vector3D normal)
    {
        Vector3D unitNormal = normal.Normalize();
        return new Plane3D(unitNormal, -Vector3D.Dot(unitNormal, point));
    }

    public double SignedDistanceTo(Vector3D point) =>
        Vector3D.Dot(Normal, point) + D;
}

/// <summary>
/// 3D ray defined by an origin and a normalized direction vector.
/// </summary>
public readonly record struct Ray3D
{
    public Vector3D Origin { get; init; }
    public Vector3D Direction { get; init; }

    public Ray3D(Vector3D origin, Vector3D direction)
    {
        Origin = origin;
        Direction = direction.Normalize();
    }

    public Vector3D GetPoint(double distance) =>
        Origin + Direction * distance;

    public bool Intersects(BoundingBox box, out double distance)
    {
        distance = 0.0;
        if (!box.IsValid || Direction.LengthSquared() <= MathUtilities.Epsilon)
        {
            return false;
        }

        double tMin = 0.0;
        double tMax = double.MaxValue;

        if (!IntersectSlab(Origin.X, Direction.X, box.Min.X, box.Max.X, ref tMin, ref tMax) ||
            !IntersectSlab(Origin.Y, Direction.Y, box.Min.Y, box.Max.Y, ref tMin, ref tMax) ||
            !IntersectSlab(Origin.Z, Direction.Z, box.Min.Z, box.Max.Z, ref tMin, ref tMax))
        {
            return false;
        }

        distance = tMin;
        return true;
    }

    private static bool IntersectSlab(
        double origin,
        double dir,
        double min,
        double max,
        ref double tMin,
        ref double tMax)
    {
        if (Math.Abs(dir) <= MathUtilities.Epsilon)
        {
            return origin >= min && origin <= max;
        }

        double invDir = 1.0 / dir;
        double t1 = (min - origin) * invDir;
        double t2 = (max - origin) * invDir;

        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);

        return tMin <= tMax;
    }
}


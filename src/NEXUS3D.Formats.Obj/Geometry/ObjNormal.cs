using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Formats.Obj.Geometry;

/// <summary>
/// Represents a Wavefront OBJ vertex normal (<c>vn x y z</c>).
/// Preserves the original declared normal without auto-normalizing unless explicitly requested.
/// </summary>
public readonly record struct ObjNormal(double X, double Y, double Z, int SourceLineNumber = 0)
{
    public bool IsFinite =>
        double.IsFinite(X) &&
        double.IsFinite(Y) &&
        double.IsFinite(Z);

    public double LengthSquared => X * X + Y * Y + Z * Z;

    public bool IsZero => LengthSquared <= MathUtilities.Epsilon;

    public bool IsValid => IsFinite && !IsZero;

    public bool IsUnitLength => IsValid && Math.Abs(LengthSquared - 1.0) <= MathUtilities.LooseEpsilon;

    public Vector3D ToVector3D() => new(X, Y, Z);

    public ObjNormal Normalize()
    {
        Vector3D v = ToVector3D().Normalize();
        return new ObjNormal(v.X, v.Y, v.Z, SourceLineNumber);
    }
}


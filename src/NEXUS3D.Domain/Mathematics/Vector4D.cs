namespace NEXUS3D.Domain.Mathematics;

/// <summary>
/// Double-precision 4D vector for homogeneous coordinates, tangents, RGBA vertex colors, and custom attributes.
/// </summary>
public readonly record struct Vector4D(double X, double Y, double Z, double W)
{
    public static Vector4D Zero => new(0.0, 0.0, 0.0, 0.0);
    public static Vector4D One => new(1.0, 1.0, 1.0, 1.0);
    public static Vector4D UnitW => new(0.0, 0.0, 0.0, 1.0);

    public Vector4D(Vector3D xyz, double w)
        : this(xyz.X, xyz.Y, xyz.Z, w)
    {
    }

    public Vector3D ToVector3D() => new(X, Y, Z);

    public double LengthSquared() => X * X + Y * Y + Z * Z + W * W;

    public double Length() => Math.Sqrt(LengthSquared());

    public bool IsFinite() =>
        double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z) && double.IsFinite(W);

    public Vector4D Normalize()
    {
        double length = Length();
        if (length <= MathUtilities.Epsilon)
        {
            return Zero;
        }

        return this / length;
    }

    public static double Dot(Vector4D a, Vector4D b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    public static Vector4D Lerp(Vector4D a, Vector4D b, double amount) =>
        new(
            MathUtilities.Lerp(a.X, b.X, amount),
            MathUtilities.Lerp(a.Y, b.Y, amount),
            MathUtilities.Lerp(a.Z, b.Z, amount),
            MathUtilities.Lerp(a.W, b.W, amount));

    public static Vector4D operator +(Vector4D a, Vector4D b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

    public static Vector4D operator -(Vector4D a, Vector4D b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

    public static Vector4D operator -(Vector4D value) =>
        new(-value.X, -value.Y, -value.Z, -value.W);

    public static Vector4D operator *(Vector4D value, double scale) =>
        new(value.X * scale, value.Y * scale, value.Z * scale, value.W * scale);

    public static Vector4D operator *(double scale, Vector4D value) =>
        value * scale;

    public static Vector4D operator /(Vector4D value, double scale)
    {
        if (Math.Abs(scale) <= MathUtilities.Epsilon)
        {
            throw new DivideByZeroException("Cannot divide Vector4D by zero.");
        }

        return new Vector4D(value.X / scale, value.Y / scale, value.Z / scale, value.W / scale);
    }
}


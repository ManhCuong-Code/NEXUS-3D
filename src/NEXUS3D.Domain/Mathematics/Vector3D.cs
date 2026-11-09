namespace NEXUS3D.Domain.Mathematics;

/// <summary>
/// Double-precision 3D vector for positions, directions, normals, and scales in USM.
/// </summary>
public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D Zero => new(0.0, 0.0, 0.0);
    public static Vector3D One => new(1.0, 1.0, 1.0);
    public static Vector3D UnitX => new(1.0, 0.0, 0.0);
    public static Vector3D UnitY => new(0.0, 1.0, 0.0);
    public static Vector3D UnitZ => new(0.0, 0.0, 1.0);

    public double LengthSquared() => X * X + Y * Y + Z * Z;

    public double Length() => Math.Sqrt(LengthSquared());

    public bool IsFinite() =>
        double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

    public Vector3D Normalize()
    {
        double length = Length();
        if (length <= MathUtilities.Epsilon)
        {
            return Zero;
        }

        return this / length;
    }

    public static double Dot(Vector3D a, Vector3D b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

    public static double DistanceSquared(Vector3D a, Vector3D b) =>
        (a - b).LengthSquared();

    public static double Distance(Vector3D a, Vector3D b) =>
        (a - b).Length();

    public static Vector3D Min(Vector3D a, Vector3D b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));

    public static Vector3D Max(Vector3D a, Vector3D b) =>
        new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));

    public static Vector3D Lerp(Vector3D a, Vector3D b, double amount) =>
        new(
            MathUtilities.Lerp(a.X, b.X, amount),
            MathUtilities.Lerp(a.Y, b.Y, amount),
            MathUtilities.Lerp(a.Z, b.Z, amount));

    public static Vector3D operator +(Vector3D a, Vector3D b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3D operator -(Vector3D a, Vector3D b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3D operator -(Vector3D value) =>
        new(-value.X, -value.Y, -value.Z);

    public static Vector3D operator *(Vector3D value, double scale) =>
        new(value.X * scale, value.Y * scale, value.Z * scale);

    public static Vector3D operator *(double scale, Vector3D value) =>
        value * scale;

    public static Vector3D MultiplyComponents(Vector3D a, Vector3D b) =>
        new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    public static Vector3D operator /(Vector3D value, double scale)
    {
        if (Math.Abs(scale) <= MathUtilities.Epsilon)
        {
            throw new DivideByZeroException("Cannot divide Vector3D by zero.");
        }

        return new Vector3D(value.X / scale, value.Y / scale, value.Z / scale);
    }
}


namespace NEXUS3D.Domain.Mathematics;

/// <summary>
/// Double-precision 2D vector used for UV texture coordinates and 2D projections.
/// </summary>
public readonly record struct Vector2D(double X, double Y)
{
    public static Vector2D Zero => new(0.0, 0.0);
    public static Vector2D One => new(1.0, 1.0);
    public static Vector2D UnitX => new(1.0, 0.0);
    public static Vector2D UnitY => new(0.0, 1.0);

    public double LengthSquared() => X * X + Y * Y;

    public double Length() => Math.Sqrt(LengthSquared());

    public bool IsFinite() => double.IsFinite(X) && double.IsFinite(Y);

    public Vector2D Normalize()
    {
        double length = Length();
        if (length <= MathUtilities.Epsilon)
        {
            return Zero;
        }

        return this / length;
    }

    public static double Dot(Vector2D a, Vector2D b) =>
        a.X * b.X + a.Y * b.Y;

    public static double Distance(Vector2D a, Vector2D b) =>
        (a - b).Length();

    public static Vector2D Lerp(Vector2D a, Vector2D b, double amount) =>
        new(
            MathUtilities.Lerp(a.X, b.X, amount),
            MathUtilities.Lerp(a.Y, b.Y, amount));

    public static Vector2D operator +(Vector2D a, Vector2D b) =>
        new(a.X + b.X, a.Y + b.Y);

    public static Vector2D operator -(Vector2D a, Vector2D b) =>
        new(a.X - b.X, a.Y - b.Y);

    public static Vector2D operator -(Vector2D value) =>
        new(-value.X, -value.Y);

    public static Vector2D operator *(Vector2D a, double scale) =>
        new(a.X * scale, a.Y * scale);

    public static Vector2D operator *(double scale, Vector2D a) =>
        a * scale;

    public static Vector2D operator /(Vector2D a, double scale)
    {
        if (Math.Abs(scale) <= MathUtilities.Epsilon)
        {
            throw new DivideByZeroException("Cannot divide Vector2D by zero.");
        }

        return new Vector2D(a.X / scale, a.Y / scale);
    }
}


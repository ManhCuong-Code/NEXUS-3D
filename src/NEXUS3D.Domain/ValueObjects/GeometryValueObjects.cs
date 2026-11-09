namespace NEXUS3D.Domain.ValueObjects;

/// <summary>
/// Immutable 3D vector value object used for model bounds, offsets, and scale metadata.
/// </summary>
public readonly record struct Vector3DValue(double X, double Y, double Z)
{
    public static Vector3DValue Zero => new(0, 0, 0);
    public static Vector3DValue One => new(1, 1, 1);

    public bool IsValid =>
        !double.IsNaN(X) && !double.IsInfinity(X) &&
        !double.IsNaN(Y) && !double.IsInfinity(Y) &&
        !double.IsNaN(Z) && !double.IsInfinity(Z);

    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
}

/// <summary>
/// Axis-aligned 3D bounding box metadata.
/// </summary>
public readonly record struct BoundingBox3D(Vector3DValue Min, Vector3DValue Max)
{
    public static BoundingBox3D Empty => new(Vector3DValue.Zero, Vector3DValue.Zero);

    public double Width => Math.Max(0, Max.X - Min.X);
    public double Height => Math.Max(0, Max.Y - Min.Y);
    public double Depth => Math.Max(0, Max.Z - Min.Z);
    public double Volume => Width * Height * Depth;

    public Vector3DValue Size => new(Width, Height, Depth);
}


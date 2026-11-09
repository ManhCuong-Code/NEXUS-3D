using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Formats.Obj.Geometry;

/// <summary>
/// Represents a Wavefront OBJ vertex position (<c>v x y z [w]</c>) preserving full double precision.
/// </summary>
public readonly record struct ObjVertex(double X, double Y, double Z, double W = 1.0, int SourceLineNumber = 0)
{
    public bool IsFinite =>
        double.IsFinite(X) &&
        double.IsFinite(Y) &&
        double.IsFinite(Z) &&
        double.IsFinite(W) &&
        Math.Abs(W) > MathUtilities.Epsilon;

    public Vector3D ToVector3D() =>
        Math.Abs(W - 1.0) <= MathUtilities.Epsilon
            ? new Vector3D(X, Y, Z)
            : new Vector3D(X / W, Y / W, Z / W);

    public Vector3D ToRawVector3D() => new(X, Y, Z);

    public Vector4D ToVector4D() => new(X, Y, Z, W);
}

using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Formats.Obj.Geometry;

/// <summary>
/// Represents a Wavefront OBJ texture coordinate (<c>vt u [v] [w]</c>), supporting 1D, 2D, and 3D UVW coordinates.
/// </summary>
public readonly record struct ObjTextureCoordinate(
    double U,
    double V = 0.0,
    double W = 0.0,
    int Dimension = 2,
    int SourceLineNumber = 0)
{
    public bool IsFinite =>
        double.IsFinite(U) &&
        double.IsFinite(V) &&
        double.IsFinite(W) &&
        Dimension is >= 1 and <= 3;

    public Vector2D ToVector2D() => new(U, V);

    public Vector3D ToVector3D() => new(U, V, W);
}

namespace NEXUS3D.Formats.Obj.Geometry;

/// <summary>
/// Represents a parameter-space vertex (<c>vp u [v] [w]</c>) for free-form curves/surfaces in OBJ.
/// </summary>
public readonly record struct ObjParameterVertex(
    double U,
    double V = 0.0,
    double W = 1.0,
    int Dimension = 1,
    int SourceLineNumber = 0)
{
    public bool IsFinite =>
        double.IsFinite(U) && double.IsFinite(V) && double.IsFinite(W);
}

/// <summary>
/// Vertex reference in an <see cref="ObjPolyline"/> (<c>l v1[/vt1] v2[/vt2] ...</c>).
/// </summary>
public readonly record struct ObjPolylineVertex(int PositionIndex, int? TextureCoordIndex = null);

/// <summary>
/// Represents a Wavefront OBJ polyline element (<c>l v1[/vt1] v2[/vt2] ...</c>).
/// </summary>
public sealed class ObjPolyline
{
    public IReadOnlyList<ObjPolylineVertex> Vertices { get; init; } = [];
    public string? ObjectName { get; init; }
    public IReadOnlyList<string> GroupNames { get; init; } = [];
    public string? MaterialName { get; init; }
    public int SourceLineNumber { get; init; }
}

/// <summary>
/// Represents a Wavefront OBJ point element (<c>p v1 v2 ...</c>).
/// </summary>
public sealed class ObjPointElement
{
    public IReadOnlyList<int> PositionIndices { get; init; } = [];
    public string? ObjectName { get; init; }
    public IReadOnlyList<string> GroupNames { get; init; } = [];
    public int SourceLineNumber { get; init; }
}


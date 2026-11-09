namespace NEXUS3D.Formats.Obj.Geometry;

/// <summary>
/// Represents a named Wavefront OBJ object (<c>o ObjectName</c>) and the geometry elements belonging to it.
/// </summary>
public sealed class ObjObject
{
    public string Name { get; init; } = string.Empty;
    public List<int> FaceIndices { get; } = [];
    public List<int> PolylineIndices { get; } = [];
    public List<int> PointIndices { get; } = [];

    public ObjObject(string name)
    {
        Name = name ?? string.Empty;
    }
}


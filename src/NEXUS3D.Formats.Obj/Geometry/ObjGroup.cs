namespace NEXUS3D.Formats.Obj.Geometry;

/// <summary>
/// Represents a named Wavefront OBJ polygon group (<c>g GroupName</c>) and the faces assigned to it.
/// </summary>
public sealed class ObjGroup
{
    public string Name { get; init; } = "default";
    public List<int> FaceIndices { get; } = [];

    public ObjGroup(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "default" : name;
    }
}


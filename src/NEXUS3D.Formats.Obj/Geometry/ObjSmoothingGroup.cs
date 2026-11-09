namespace NEXUS3D.Formats.Obj.Geometry;

/// <summary>
/// Represents a Wavefront OBJ smoothing group (<c>s 1</c>, <c>s 2</c>, <c>s off</c>, <c>s 0</c>).
/// GroupId <c>0</c> denotes smoothing disabled (<c>s off</c> or <c>s 0</c>).
/// </summary>
public sealed class ObjSmoothingGroup
{
    public int GroupId { get; init; }
    public bool IsEnabled => GroupId > 0;
    public List<int> FaceIndices { get; } = [];

    public ObjSmoothingGroup(int groupId)
    {
        GroupId = Math.Max(0, groupId);
    }
}


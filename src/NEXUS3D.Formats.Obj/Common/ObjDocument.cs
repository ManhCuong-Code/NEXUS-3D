using NEXUS3D.Domain.Metadata;
using NEXUS3D.Formats.Obj.Geometry;

namespace NEXUS3D.Formats.Obj.Common;

/// <summary>
/// Preserved unrecognized or custom OBJ statement with its line number.
/// </summary>
public readonly record struct ObjUnrecognizedStatement(int LineNumber, string Keyword, string RawLine);

/// <summary>
/// Intermediate Wavefront OBJ document preserving all independent vertex positions (<c>v</c>),
/// texture coordinates (<c>vt</c>), vertex normals (<c>vn</c>), parameter vertices (<c>vp</c>),
/// polygon faces (<c>f</c>), polylines (<c>l</c>), points (<c>p</c>), objects (<c>o</c>),
/// groups (<c>g</c>), smoothing groups (<c>s</c>), material library references (<c>mtllib</c>),
/// comments (<c>#</c>), and unrecognized statements.
/// </summary>
public sealed class ObjDocument
{
    public string? SourceFileName { get; set; }
    public List<ObjVertex> Positions { get; } = [];
    public List<ObjTextureCoordinate> TextureCoordinates { get; } = [];
    public List<ObjNormal> Normals { get; } = [];
    public List<ObjParameterVertex> ParameterVertices { get; } = [];
    public List<ObjFace> Faces { get; } = [];
    public List<ObjPolyline> Polylines { get; } = [];
    public List<ObjPointElement> Points { get; } = [];

    public List<string> MaterialLibraries { get; } = [];
    public Dictionary<string, ObjObject> Objects { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, ObjGroup> Groups { get; } = new(StringComparer.Ordinal);
    public Dictionary<int, ObjSmoothingGroup> SmoothingGroups { get; } = [];
    public List<string> HeaderComments { get; } = [];
    public List<ObjUnrecognizedStatement> UnrecognizedStatements { get; } = [];
    public List<WarningRecord> Warnings { get; } = [];

    public bool HasGeometry =>
        Positions.Count > 0 || Faces.Count > 0 || Polylines.Count > 0 || Points.Count > 0;
}


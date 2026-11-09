using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Meshes;

/// <summary>
/// Topology mode of a <see cref="MeshData"/> primitive stream.
/// </summary>
public enum MeshTopology
{
    Points = 0,
    Lines = 1,
    Triangles = 2,
    Polygons = 3
}

/// <summary>
/// Named custom vertex attribute (e.g., secondary UV, tangent, AO, or custom shader channel).
/// </summary>
public readonly record struct VertexAttribute(string Name, Vector4D Value);

/// <summary>
/// Represents a single 3D mesh vertex. Normal, TexCoord (UV), and Color are optional.
/// </summary>
public sealed class Vertex : IDeepCloneable<Vertex>
{
    public Vector3D Position { get; set; }
    public Vector3D? Normal { get; set; }
    public Vector2D? TexCoord { get; set; }
    public Vector4D? Color { get; set; }
    public Dictionary<string, Vector4D> CustomAttributes { get; set; } = new(StringComparer.Ordinal);

    public Vertex()
    {
    }

    public Vertex(
        Vector3D position,
        Vector3D? normal = null,
        Vector2D? texCoord = null,
        Vector4D? color = null)
    {
        Position = position;
        Normal = normal;
        TexCoord = texCoord;
        Color = color;
    }

    public Vertex DeepClone() => new(Position, Normal, TexCoord, Color)
    {
        CustomAttributes = new Dictionary<string, Vector4D>(CustomAttributes, StringComparer.Ordinal)
    };
}

/// <summary>
/// Represents an independent index tuple (<c>PositionIndex</c>, <c>TexCoordIndex</c>, <c>NormalIndex</c>)
/// for a single corner of a polygon <see cref="Face"/> when preserved from formats with decoupled attribute indexing (such as Wavefront OBJ).
/// </summary>
public readonly record struct FaceCorner(
    int PositionIndex,
    int? TexCoordIndex = null,
    int? NormalIndex = null);

/// <summary>
/// Represents a polygon or triangle face indexing into <see cref="MeshData.Vertices"/>.
/// Does not force triangulation in the Domain model.
/// </summary>
public sealed class Face : IDeepCloneable<Face>
{
    public IReadOnlyList<int> VertexIndices { get; set; } = [];
    public Guid? MaterialId { get; set; }
    public int? SmoothingGroup { get; set; }
    public IReadOnlyList<FaceCorner>? Corners { get; set; }
    public IReadOnlyList<string> GroupNames { get; set; } = [];

    public bool IsTriangle => VertexIndices.Count == 3;
    public bool IsQuad => VertexIndices.Count == 4;
    public bool IsPolygon => VertexIndices.Count >= 3;

    public Face()
    {
    }

    public Face(IEnumerable<int> vertexIndices, Guid? materialId = null, int? smoothingGroup = null)
    {
        DomainGuard.NotNull(vertexIndices);
        VertexIndices = vertexIndices.ToArray();
        MaterialId = materialId;
        SmoothingGroup = smoothingGroup;
    }

    public bool HasDuplicateIndices()
    {
        if (VertexIndices.Count <= 1)
        {
            return false;
        }

        for (int i = 0; i < VertexIndices.Count; i++)
        {
            for (int j = i + 1; j < VertexIndices.Count; j++)
            {
                if (VertexIndices[i] == VertexIndices[j])
                {
                    return true;
                }
            }
        }

        return false;
    }

    public Face DeepClone() => new(VertexIndices.ToArray(), MaterialId, SmoothingGroup)
    {
        Corners = Corners?.ToArray(),
        GroupNames = GroupNames.Count > 0 ? GroupNames.ToArray() : []
    };
}

/// <summary>
/// Geometric 3D triangle primitive used for area, normal, and degeneracy analysis.
/// </summary>
public readonly record struct Triangle(Vector3D A, Vector3D B, Vector3D C)
{
    public Vector3D CrossProduct => Vector3D.Cross(B - A, C - A);

    public double Area => 0.5 * CrossProduct.Length();

    public Vector3D Normal => CrossProduct.Normalize();

    public bool IsDegenerate => Area <= MathUtilities.Epsilon;
}


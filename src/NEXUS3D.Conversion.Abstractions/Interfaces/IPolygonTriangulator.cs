using NEXUS3D.Domain.Meshes;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Service converting quads and n-gons into planar triangle faces.
/// </summary>
public interface IPolygonTriangulator
{
    IReadOnlyList<Face> TriangulateFace(Face face, IReadOnlyList<Vertex> vertices);
    MeshData TriangulateMesh(MeshData sourceMesh);
}

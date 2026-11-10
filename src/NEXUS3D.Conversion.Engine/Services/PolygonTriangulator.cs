using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Converts quads and complex n-gon polygons into planar triangle faces using fan splitting
/// and 3D projected ear-clipping algorithms.
/// </summary>
public sealed class PolygonTriangulator : IPolygonTriangulator
{
    public IReadOnlyList<Face> TriangulateFace(Face face, IReadOnlyList<Vertex> vertices)
    {
        Guard.NotNull(face);
        Guard.NotNull(vertices);

        int count = face.VertexIndices.Count;
        if (count < 3)
        {
            return [];
        }

        if (count == 3)
        {
            return [face.DeepClone()];
        }

        if (count == 4)
        {
            return TriangulateQuad(face, vertices);
        }

        return TriangulateNgon(face, vertices);
    }

    public MeshData TriangulateMesh(MeshData sourceMesh)
    {
        Guard.NotNull(sourceMesh);

        var triangulated = new MeshData(sourceMesh.Name, MeshTopology.Triangles);

        // Clone all vertices
        for (int i = 0; i < sourceMesh.Vertices.Count; i++)
        {
            triangulated.Vertices.Add(sourceMesh.Vertices[i].DeepClone());
        }

        // Triangulate each face
        for (int i = 0; i < sourceMesh.Faces.Count; i++)
        {
            var face = sourceMesh.Faces[i];
            var triFaces = TriangulateFace(face, sourceMesh.Vertices);
            for (int t = 0; t < triFaces.Count; t++)
            {
                triangulated.Faces.Add(triFaces[t]);
            }
        }

        return triangulated;
    }

    private static IReadOnlyList<Face> TriangulateQuad(Face quadFace, IReadOnlyList<Vertex> vertices)
    {
        int i0 = quadFace.VertexIndices[0];
        int i1 = quadFace.VertexIndices[1];
        int i2 = quadFace.VertexIndices[2];
        int i3 = quadFace.VertexIndices[3];

        // Diagonal split comparison: (i0-i2) vs (i1-i3)
        Vector3D p0 = vertices[i0].Position;
        Vector3D p1 = vertices[i1].Position;
        Vector3D p2 = vertices[i2].Position;
        Vector3D p3 = vertices[i3].Position;

        double d02 = (p2 - p0).LengthSquared();
        double d13 = (p3 - p1).LengthSquared();

        Face tri1;
        Face tri2;

        if (d02 <= d13)
        {
            // Split along 0-2: (0, 1, 2) and (0, 2, 3)
            tri1 = new Face([i0, i1, i2], quadFace.MaterialId, quadFace.SmoothingGroup)
            {
                GroupNames = quadFace.GroupNames.ToArray()
            };
            tri2 = new Face([i0, i2, i3], quadFace.MaterialId, quadFace.SmoothingGroup)
            {
                GroupNames = quadFace.GroupNames.ToArray()
            };

            if (quadFace.Corners is { Count: 4 } c)
            {
                tri1.Corners = [c[0], c[1], c[2]];
                tri2.Corners = [c[0], c[2], c[3]];
            }
        }
        else
        {
            // Split along 1-3: (0, 1, 3) and (1, 2, 3)
            tri1 = new Face([i0, i1, i3], quadFace.MaterialId, quadFace.SmoothingGroup)
            {
                GroupNames = quadFace.GroupNames.ToArray()
            };
            tri2 = new Face([i1, i2, i3], quadFace.MaterialId, quadFace.SmoothingGroup)
            {
                GroupNames = quadFace.GroupNames.ToArray()
            };

            if (quadFace.Corners is { Count: 4 } c)
            {
                tri1.Corners = [c[0], c[1], c[3]];
                tri2.Corners = [c[1], c[2], c[3]];
            }
        }

        return [tri1, tri2];
    }

    private static IReadOnlyList<Face> TriangulateNgon(Face ngonFace, IReadOnlyList<Vertex> vertices)
    {
        var indices = ngonFace.VertexIndices.ToList();
        var corners = ngonFace.Corners?.ToList();
        var result = new List<Face>();

        // Ear-clipping algorithm with fallback to fan triangulation
        int n = indices.Count;
        if (n < 3) return result;

        // Compute face normal using Newell's method
        Vector3D normal = Vector3D.Zero;
        for (int i = 0; i < n; i++)
        {
            Vector3D current = vertices[indices[i]].Position;
            Vector3D next = vertices[indices[(i + 1) % n]].Position;
            normal = new Vector3D(
                normal.X + (current.Y - next.Y) * (current.Z + next.Z),
                normal.Y + (current.Z - next.Z) * (current.X + next.X),
                normal.Z + (current.X - next.X) * (current.Y + next.Y));
        }

        if (normal.LengthSquared() < 1e-12)
        {
            // Degenerate normal, fallback to simple fan
            return FallbackFan(ngonFace);
        }

        normal = normal.Normalize();

        // Project 3D points onto 2D plane perpendicular to dominant axis
        int dropAxis = 0; // 0=X, 1=Y, 2=Z
        double absX = Math.Abs(normal.X);
        double absY = Math.Abs(normal.Y);
        double absZ = Math.Abs(normal.Z);

        if (absY > absX && absY > absZ) dropAxis = 1;
        else if (absZ > absX && absZ > absY) dropAxis = 2;

        List<int> polyIndices = [.. Enumerable.Range(0, n)];
        int safeguard = n * n * 2;

        while (polyIndices.Count > 3 && safeguard-- > 0)
        {
            bool earFound = false;

            for (int i = 0; i < polyIndices.Count; i++)
            {
                int prev = polyIndices[(i - 1 + polyIndices.Count) % polyIndices.Count];
                int curr = polyIndices[i];
                int next = polyIndices[(i + 1) % polyIndices.Count];

                Vector2D a = Project(vertices[indices[prev]].Position, dropAxis);
                Vector2D b = Project(vertices[indices[curr]].Position, dropAxis);
                Vector2D c = Project(vertices[indices[next]].Position, dropAxis);

                // Convex check
                double cross = Cross2D(b - a, c - a);
                if (cross <= 1e-9) continue;

                // Point-in-triangle check
                bool containsOther = false;
                for (int j = 0; j < polyIndices.Count; j++)
                {
                    int testIdx = polyIndices[j];
                    if (testIdx == prev || testIdx == curr || testIdx == next) continue;

                    Vector2D p = Project(vertices[indices[testIdx]].Position, dropAxis);
                    if (IsPointInTriangle2D(p, a, b, c))
                    {
                        containsOther = true;
                        break;
                    }
                }

                if (!containsOther)
                {
                    // Ear found
                    var triFace = new Face([indices[prev], indices[curr], indices[next]], ngonFace.MaterialId, ngonFace.SmoothingGroup)
                    {
                        GroupNames = ngonFace.GroupNames.ToArray()
                    };

                    if (corners is not null)
                    {
                        triFace.Corners = [corners[prev], corners[curr], corners[next]];
                    }

                    result.Add(triFace);
                    polyIndices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound) break;
        }

        // Remaining 3 indices
        if (polyIndices.Count == 3)
        {
            int i0 = indices[polyIndices[0]];
            int i1 = indices[polyIndices[1]];
            int i2 = indices[polyIndices[2]];

            var lastTri = new Face([i0, i1, i2], ngonFace.MaterialId, ngonFace.SmoothingGroup)
            {
                GroupNames = ngonFace.GroupNames.ToArray()
            };

            if (corners is not null)
            {
                lastTri.Corners = [corners[polyIndices[0]], corners[polyIndices[1]], corners[polyIndices[2]]];
            }

            result.Add(lastTri);
        }
        else if (result.Count == 0)
        {
            // If ear clipping failed entirely, fallback to fan
            return FallbackFan(ngonFace);
        }

        return result;
    }

    private static IReadOnlyList<Face> FallbackFan(Face ngonFace)
    {
        var result = new List<Face>();
        int count = ngonFace.VertexIndices.Count;
        int rootIndex = ngonFace.VertexIndices[0];
        var corners = ngonFace.Corners;

        for (int i = 1; i < count - 1; i++)
        {
            int i1 = ngonFace.VertexIndices[i];
            int i2 = ngonFace.VertexIndices[i + 1];

            var tri = new Face([rootIndex, i1, i2], ngonFace.MaterialId, ngonFace.SmoothingGroup)
            {
                GroupNames = ngonFace.GroupNames.ToArray()
            };

            if (corners is { Count: >= 3 })
            {
                tri.Corners = [corners[0], corners[i], corners[i + 1]];
            }

            result.Add(tri);
        }

        return result;
    }

    private static Vector2D Project(Vector3D v, int dropAxis) => dropAxis switch
    {
        0 => new Vector2D(v.Y, v.Z),
        1 => new Vector2D(v.X, v.Z),
        _ => new Vector2D(v.X, v.Y)
    };

    private static double Cross2D(Vector2D a, Vector2D b) => a.X * b.Y - a.Y * b.X;

    private static bool IsPointInTriangle2D(Vector2D p, Vector2D a, Vector2D b, Vector2D c)
    {
        double c1 = Cross2D(b - a, p - a);
        double c2 = Cross2D(c - b, p - b);
        double c3 = Cross2D(a - c, p - c);

        bool hasNeg = c1 < 0 || c2 < 0 || c3 < 0;
        bool hasPos = c1 > 0 || c2 > 0 || c3 > 0;

        return !(hasNeg && hasPos);
    }
}

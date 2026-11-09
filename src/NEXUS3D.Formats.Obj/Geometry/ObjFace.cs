using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Formats.Obj.Geometry;

/// <summary>
/// Represents a Wavefront OBJ polygon face (<c>f v1[/vt1][/vn1] v2[/vt2][/vn2] ...</c>).
/// Preserves the exact original polygon (triangle, quad, or N-gon) along with its assigned object,
/// groups, smoothing group, and material name.
/// Provides Ear-Clipping Triangulation for both convex and concave polygons when triangulation is requested.
/// </summary>
public sealed class ObjFace
{
    public IReadOnlyList<ObjFaceVertex> Vertices { get; init; } = [];
    public string? ObjectName { get; init; }
    public IReadOnlyList<string> GroupNames { get; init; } = [];
    public int SmoothingGroup { get; init; }
    public string? MaterialName { get; init; }
    public int SourceLineNumber { get; init; }

    public bool IsTriangle => Vertices.Count == 3;
    public bool IsQuad => Vertices.Count == 4;
    public bool IsNGon => Vertices.Count > 4;

    /// <summary>
    /// Computes the geometric face normal using Newell's method across all polygon vertices,
    /// respecting the polygon's right-handed counter-clockwise winding order.
    /// </summary>
    public Vector3D ComputeFaceNormal(IReadOnlyList<ObjVertex> documentPositions)
    {
        ArgumentNullException.ThrowIfNull(documentPositions);
        if (Vertices.Count < 3)
        {
            return Vector3D.Zero;
        }

        double nx = 0.0, ny = 0.0, nz = 0.0;
        int n = Vertices.Count;

        for (int i = 0; i < n; i++)
        {
            Vector3D curr = documentPositions[Vertices[i].PositionIndex].ToRawVector3D();
            Vector3D next = documentPositions[Vertices[(i + 1) % n].PositionIndex].ToRawVector3D();

            nx += (curr.Y - next.Y) * (curr.Z + next.Z);
            ny += (curr.Z - next.Z) * (curr.X + next.X);
            nz += (curr.X - next.X) * (curr.Y + next.Y);
        }

        var normal = new Vector3D(nx, ny, nz);
        return normal.LengthSquared() > MathUtilities.Epsilon ? normal.Normalize() : Vector3D.Zero;
    }

    /// <summary>
    /// Triangulates this polygon into triangles using 2D projected Ear-Clipping so concave polygons
    /// are triangulated accurately without invalid overlapping triangles.
    /// </summary>
    public IReadOnlyList<ObjFace> Triangulate(IReadOnlyList<ObjVertex> documentPositions)
    {
        ArgumentNullException.ThrowIfNull(documentPositions);

        if (Vertices.Count <= 3)
        {
            return [this];
        }

        Vector3D normal = ComputeFaceNormal(documentPositions);
        if (normal.LengthSquared() <= MathUtilities.Epsilon)
        {
            // Fallback fan for collinear/degenerate polygon
            return FanTriangulate();
        }

        // Build 2D orthonormal basis (tangent U, bitangent V) on the polygon plane
        Vector3D up = Math.Abs(normal.X) < 0.9 ? Vector3D.UnitX : Vector3D.UnitY;
        Vector3D basisU = Vector3D.Cross(normal, up).Normalize();
        Vector3D basisV = Vector3D.Cross(normal, basisU).Normalize();

        int count = Vertices.Count;
        var projected = new Vector2D[count];
        for (int i = 0; i < count; i++)
        {
            Vector3D p = documentPositions[Vertices[i].PositionIndex].ToRawVector3D();
            projected[i] = new Vector2D(Vector3D.Dot(p, basisU), Vector3D.Dot(p, basisV));
        }

        // Ensure signed 2D area is positive (CCW in 2D basis)
        double signedArea2 = 0.0;
        for (int i = 0; i < count; i++)
        {
            Vector2D a = projected[i];
            Vector2D b = projected[(i + 1) % count];
            signedArea2 += a.X * b.Y - b.X * a.Y;
        }

        double windingSign = signedArea2 >= 0.0 ? 1.0 : -1.0;

        var remaining = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            remaining.Add(i);
        }

        var triangles = new List<ObjFace>(count - 2);
        int maxIterations = count * count * 2;
        int iter = 0;

        while (remaining.Count > 3 && iter++ < maxIterations)
        {
            bool earFound = false;
            int remCount = remaining.Count;

            for (int i = 0; i < remCount; i++)
            {
                int prevIdx = remaining[(i - 1 + remCount) % remCount];
                int currIdx = remaining[i];
                int nextIdx = remaining[(i + 1) % remCount];

                Vector2D a = projected[prevIdx];
                Vector2D b = projected[currIdx];
                Vector2D c = projected[nextIdx];

                double cross = Cross2D(a, b, c) * windingSign;
                if (cross <= MathUtilities.Epsilon)
                {
                    // Reflex or collinear vertex -> not a convex ear
                    continue;
                }

                bool containsOtherPoint = false;
                for (int j = 0; j < remCount; j++)
                {
                    int testIdx = remaining[j];
                    if (testIdx == prevIdx || testIdx == currIdx || testIdx == nextIdx)
                    {
                        continue;
                    }

                    if (PointInTriangle2D(projected[testIdx], a, b, c))
                    {
                        containsOtherPoint = true;
                        break;
                    }
                }

                if (!containsOtherPoint)
                {
                    triangles.Add(CreateTriangleFace(Vertices[prevIdx], Vertices[currIdx], Vertices[nextIdx]));
                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound)
            {
                // Relaxed pass: clip best available triangle if self-intersecting/near-collinear
                int prevIdx = remaining[0];
                int currIdx = remaining[1];
                int nextIdx = remaining[2];
                triangles.Add(CreateTriangleFace(Vertices[prevIdx], Vertices[currIdx], Vertices[nextIdx]));
                remaining.RemoveAt(1);
            }
        }

        if (remaining.Count == 3)
        {
            triangles.Add(CreateTriangleFace(
                Vertices[remaining[0]],
                Vertices[remaining[1]],
                Vertices[remaining[2]]));
        }

        return triangles;
    }

    private List<ObjFace> FanTriangulate()
    {
        var list = new List<ObjFace>(Vertices.Count - 2);
        for (int i = 1; i < Vertices.Count - 1; i++)
        {
            list.Add(CreateTriangleFace(Vertices[0], Vertices[i], Vertices[i + 1]));
        }
        return list;
    }

    private ObjFace CreateTriangleFace(ObjFaceVertex v0, ObjFaceVertex v1, ObjFaceVertex v2) =>
        new()
        {
            Vertices = [v0, v1, v2],
            ObjectName = ObjectName,
            GroupNames = GroupNames,
            SmoothingGroup = SmoothingGroup,
            MaterialName = MaterialName,
            SourceLineNumber = SourceLineNumber
        };

    private static double Cross2D(Vector2D a, Vector2D b, Vector2D c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool PointInTriangle2D(Vector2D p, Vector2D a, Vector2D b, Vector2D c)
    {
        double d1 = Cross2D(a, b, p);
        double d2 = Cross2D(b, c, p);
        double d3 = Cross2D(c, a, p);

        bool hasNeg = (d1 < -MathUtilities.Epsilon) || (d2 < -MathUtilities.Epsilon) || (d3 < -MathUtilities.Epsilon);
        bool hasPos = (d1 > MathUtilities.Epsilon) || (d2 > MathUtilities.Epsilon) || (d3 > MathUtilities.Epsilon);

        return !(hasNeg && hasPos);
    }
}

using System.Numerics;

namespace NEXUS3D.Voxelization.Engine.Geometry;

/// <summary>
/// Implements Tomas Akenine-Möller's Fast Triangle-AABB (Axis Aligned Bounding Box) Overlap Test
/// using the Separating Axis Theorem (SAT) across 13 potential separating axes:
/// - 3 box face normals (AABB projection)
/// - 1 triangle normal (plane-box intersection)
/// - 9 cross products of box edges and triangle edges (edge-edge separation)
/// </summary>
public static class TriangleBoxIntersection
{
    /// <summary>
    /// Tests whether the triangle defined by vertices (v0, v1, v2) intersects the AABB centered at boxCenter with halfSize.
    /// </summary>
    public static bool Intersects(
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 boxCenter,
        Vector3 boxHalfSize,
        float expansion = 0.0f)
    {
        // 1. Translate triangle vertices so box center is at origin
        var p0 = v0 - boxCenter;
        var p1 = v1 - boxCenter;
        var p2 = v2 - boxCenter;

        var extents = boxHalfSize + new Vector3(expansion);

        // 2. Test 3 Box face normals (AABB axes: X, Y, Z)
        // Find min and max of triangle along X, Y, Z
        float minX = MathF.Min(p0.X, MathF.Min(p1.X, p2.X));
        float maxX = MathF.Max(p0.X, MathF.Max(p1.X, p2.X));
        if (minX > extents.X || maxX < -extents.X) return false;

        float minY = MathF.Min(p0.Y, MathF.Min(p1.Y, p2.Y));
        float maxY = MathF.Max(p0.Y, MathF.Max(p1.Y, p2.Y));
        if (minY > extents.Y || maxY < -extents.Y) return false;

        float minZ = MathF.Min(p0.Z, MathF.Min(p1.Z, p2.Z));
        float maxZ = MathF.Max(p0.Z, MathF.Max(p1.Z, p2.Z));
        if (minZ > extents.Z || maxZ < -extents.Z) return false;

        // 3. Compute triangle edge vectors
        var e0 = p1 - p0;
        var e1 = p2 - p1;
        var e2 = p0 - p2;

        // 4. Test 9 edge-cross-product axes
        // Axis 0: u0 x e0 = (0, -e0.Z, e0.Y)
        if (!AxisTestX01(e0.Z, e0.Y, p0.Y, p0.Z, p2.Y, p2.Z, extents.Y, extents.Z)) return false;
        // Axis 1: u0 x e1 = (0, -e1.Z, e1.Y)
        if (!AxisTestX01(e1.Z, e1.Y, p0.Y, p0.Z, p1.Y, p1.Z, extents.Y, extents.Z)) return false;
        // Axis 2: u0 x e2 = (0, -e2.Z, e2.Y)
        if (!AxisTestX2(e2.Z, e2.Y, p0.Y, p0.Z, p1.Y, p1.Z, extents.Y, extents.Z)) return false;

        // Axis 3: u1 x e0 = (e0.Z, 0, -e0.X)
        if (!AxisTestY02(e0.Z, e0.X, p0.X, p0.Z, p2.X, p2.Z, extents.X, extents.Z)) return false;
        // Axis 4: u1 x e1 = (e1.Z, 0, -e1.X)
        if (!AxisTestY02(e1.Z, e1.X, p0.X, p0.Z, p1.X, p1.Z, extents.X, extents.Z)) return false;
        // Axis 5: u1 x e2 = (e2.Z, 0, -e2.X)
        if (!AxisTestY1(e2.Z, e2.X, p0.X, p0.Z, p1.X, p1.Z, extents.X, extents.Z)) return false;

        // Axis 6: u2 x e0 = (-e0.Y, e0.X, 0)
        if (!AxisTestZ12(e0.Y, e0.X, p1.X, p1.Y, p2.X, p2.Y, extents.X, extents.Y)) return false;
        // Axis 7: u2 x e1 = (-e1.Y, e1.X, 0)
        if (!AxisTestZ0(e1.Y, e1.X, p0.X, p0.Y, p1.X, p1.Y, extents.X, extents.Y)) return false;
        // Axis 8: u2 x e2 = (-e2.Y, e2.X, 0)
        if (!AxisTestZ0(e2.Y, e2.X, p0.X, p0.Y, p1.X, p1.Y, extents.X, extents.Y)) return false;

        // 5. Test 1 Triangle normal axis (Plane-AABB overlap)
        var normal = Vector3.Cross(e0, e1);
        if (!PlaneBoxOverlap(normal, p0, extents)) return false;

        // Passed all 13 separating axis tests -> overlap exists
        return true;
    }

    private static bool AxisTestX01(float a, float b, float fa, float fb, float p2Y, float p2Z, float extY, float extZ)
    {
        float p0 = a * fa - b * fb;
        float p2 = a * p2Y - b * p2Z;
        float min = MathF.Min(p0, p2);
        float max = MathF.Max(p0, p2);
        float rad = MathF.Abs(a) * extY + MathF.Abs(b) * extZ;
        return min <= rad && max >= -rad;
    }

    private static bool AxisTestX2(float a, float b, float fa, float fb, float p1Y, float p1Z, float extY, float extZ)
    {
        float p0 = a * fa - b * fb;
        float p1 = a * p1Y - b * p1Z;
        float min = MathF.Min(p0, p1);
        float max = MathF.Max(p0, p1);
        float rad = MathF.Abs(a) * extY + MathF.Abs(b) * extZ;
        return min <= rad && max >= -rad;
    }

    private static bool AxisTestY02(float a, float b, float fa, float fb, float p2X, float p2Z, float extX, float extZ)
    {
        float p0 = -a * fa + b * fb;
        float p2 = -a * p2X + b * p2Z;
        float min = MathF.Min(p0, p2);
        float max = MathF.Max(p0, p2);
        float rad = MathF.Abs(a) * extX + MathF.Abs(b) * extZ;
        return min <= rad && max >= -rad;
    }

    private static bool AxisTestY1(float a, float b, float fa, float fb, float p1X, float p1Z, float extX, float extZ)
    {
        float p0 = -a * fa + b * fb;
        float p1 = -a * p1X + b * p1Z;
        float min = MathF.Min(p0, p1);
        float max = MathF.Max(p0, p1);
        float rad = MathF.Abs(a) * extX + MathF.Abs(b) * extZ;
        return min <= rad && max >= -rad;
    }

    private static bool AxisTestZ12(float a, float b, float p1X, float p1Y, float p2X, float p2Y, float extX, float extY)
    {
        float p1 = -a * p1X + b * p1Y;
        float p2 = -a * p2X + b * p2Y;
        float min = MathF.Min(p1, p2);
        float max = MathF.Max(p1, p2);
        float rad = MathF.Abs(a) * extX + MathF.Abs(b) * extY;
        return min <= rad && max >= -rad;
    }

    private static bool AxisTestZ0(float a, float b, float p0X, float p0Y, float p1X, float p1Y, float extX, float extY)
    {
        float p0 = -a * p0X + b * p0Y;
        float p1 = -a * p1X + b * p1Y;
        float min = MathF.Min(p0, p1);
        float max = MathF.Max(p0, p1);
        float rad = MathF.Abs(a) * extX + MathF.Abs(b) * extY;
        return min <= rad && max >= -rad;
    }

    private static bool PlaneBoxOverlap(Vector3 normal, Vector3 pointOnPlane, Vector3 extents)
    {
        var vmin = new Vector3(
            normal.X > 0f ? -extents.X : extents.X,
            normal.Y > 0f ? -extents.Y : extents.Y,
            normal.Z > 0f ? -extents.Z : extents.Z);

        var vmax = new Vector3(
            normal.X > 0f ? extents.X : -extents.X,
            normal.Y > 0f ? extents.Y : -extents.Y,
            normal.Z > 0f ? extents.Z : -extents.Z);

        if (Vector3.Dot(normal, vmin - pointOnPlane) > 0f) return false;
        if (Vector3.Dot(normal, vmax - pointOnPlane) >= 0f) return true;

        return false;
    }
}

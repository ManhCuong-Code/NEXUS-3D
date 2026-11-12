using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.Editor.Engine.Picking;

/// <summary>
/// Raycasting picker supporting ray-mesh triangle intersection via Möller–Trumbore and ray-voxel bounds intersection.
/// </summary>
public sealed class RaycastPicker : IRaycastPicker
{
    public RaycastHitResult? Pick(Scene3D scene, Ray3D ray)
    {
        var allHits = PickAll(scene, ray);
        return allHits.Count > 0 ? allHits[0] : null;
    }

    public IReadOnlyList<RaycastHitResult> PickAll(Scene3D scene, Ray3D ray)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var hits = new List<RaycastHitResult>();
        TraverseNode(scene.Graph.Root, ray, hits);
        hits.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return hits;
    }

    private static void TraverseNode(SceneNode node, Ray3D worldRay, List<RaycastHitResult> hits)
    {
        if (!node.IsVisible) return;

        var worldMatrix = node.GetWorldMatrix();

        if (node.Object is not null)
        {
            if (node.Object.Meshes.Count > 0)
            {
                PickMeshObject(node, node.Object.Meshes, worldMatrix, worldRay, hits);
            }

            if (node.Object.VoxelGrids.Count > 0)
            {
                PickVoxelObject(node, node.Object.VoxelGrids, worldMatrix, worldRay, hits);
            }
        }

        foreach (var child in node.Children)
        {
            TraverseNode(child, worldRay, hits);
        }
    }

    private static void PickMeshObject(
        SceneNode node,
        IReadOnlyList<MeshData> meshes,
        Matrix4x4D worldMatrix,
        Ray3D worldRay,
        List<RaycastHitResult> hits)
    {
        if (!worldMatrix.TryInvert(out var invWorld))
        {
            return;
        }

        // Transform ray to local space for fast face testing
        var localOrigin = invWorld.TransformPoint(worldRay.Origin);
        var localDir = invWorld.TransformDirection(worldRay.Direction).Normalize();
        var localRay = new Ray3D(localOrigin, localDir);

        foreach (var mesh in meshes)
        {
            if (mesh.Vertices.Count < 3) continue;

            // Bounding box early rejection
            var meshBounds = mesh.Bounds;
            if (!localRay.Intersects(meshBounds, out _))
            {
                continue;
            }

            for (int f = 0; f < mesh.Faces.Count; f++)
            {
                var face = mesh.Faces[f];
                var indices = face.VertexIndices;
                if (indices.Count < 3) continue;

                // Triangulate polygon via fan
                for (int i = 1; i < indices.Count - 1; i++)
                {
                    int i0 = indices[0];
                    int i1 = indices[i];
                    int i2 = indices[i + 1];

                    if (i0 >= mesh.Vertices.Count || i1 >= mesh.Vertices.Count || i2 >= mesh.Vertices.Count)
                    {
                        continue;
                    }

                    var v0 = mesh.Vertices[i0].Position;
                    var v1 = mesh.Vertices[i1].Position;
                    var v2 = mesh.Vertices[i2].Position;

                    if (IntersectRayTriangle(localRay, v0, v1, v2, out double localDist, out Vector3D localNormal))
                    {
                        var localHitPoint = localRay.GetPoint(localDist);
                        var worldHitPoint = worldMatrix.TransformPoint(localHitPoint);
                        var worldNormal = worldMatrix.TransformNormal(localNormal).Normalize();
                        double worldDist = (worldHitPoint - worldRay.Origin).Length();

                        hits.Add(new RaycastHitResult(
                            node.Id,
                            mesh.Id,
                            worldDist,
                            worldHitPoint,
                            worldNormal,
                            f));
                    }
                }
            }
        }
    }

    private static void PickVoxelObject(
        SceneNode node,
        IReadOnlyList<VoxelGrid> voxelGrids,
        Matrix4x4D worldMatrix,
        Ray3D worldRay,
        List<RaycastHitResult> hits)
    {
        if (!worldMatrix.TryInvert(out var invWorld))
        {
            return;
        }

        var localOrigin = invWorld.TransformPoint(worldRay.Origin);
        var localDir = invWorld.TransformDirection(worldRay.Direction).Normalize();
        var localRay = new Ray3D(localOrigin, localDir);

        foreach (var grid in voxelGrids)
        {
            if (grid.GetVoxelCount() == 0) continue;

            var aabb = grid.GetWorldBounds();
            if (!aabb.IsValid) continue;

            if (IntersectBox(localRay, aabb, out double tNear, out double tFar) && tFar >= 0.0)
            {
                var hit = PickVoxelInGrid(node, grid, worldMatrix, worldRay, localRay, tNear, tFar);
                if (hit is not null)
                {
                    hits.Add(hit);
                }
            }
        }
    }

    private static RaycastHitResult? PickVoxelInGrid(
        SceneNode node,
        VoxelGrid grid,
        Matrix4x4D worldMatrix,
        Ray3D worldRay,
        Ray3D localRay,
        double tNear,
        double tFar)
    {
        double vx = grid.VoxelSize.X <= 0 ? 1.0 : grid.VoxelSize.X;
        double vy = grid.VoxelSize.Y <= 0 ? 1.0 : grid.VoxelSize.Y;
        double vz = grid.VoxelSize.Z <= 0 ? 1.0 : grid.VoxelSize.Z;

        double tStart = Math.Max(0.0, tNear);
        double tEnter = tStart > 0.0 ? tStart + 1e-6 : 0.0;
        var startPt = localRay.GetPoint(tEnter);

        int curX = (int)Math.Floor(startPt.X / vx) - grid.Origin.X;
        int curY = (int)Math.Floor(startPt.Y / vy) - grid.Origin.Y;
        int curZ = (int)Math.Floor(startPt.Z / vz) - grid.Origin.Z;

        if (tStart > 0.0)
        {
            curX = Math.Clamp(curX, grid.Bounds.Min.X, grid.Bounds.Max.X);
            curY = Math.Clamp(curY, grid.Bounds.Min.Y, grid.Bounds.Max.Y);
            curZ = Math.Clamp(curZ, grid.Bounds.Min.Z, grid.Bounds.Max.Z);
        }

        double dirX = localRay.Direction.X;
        double dirY = localRay.Direction.Y;
        double dirZ = localRay.Direction.Z;

        int stepX = dirX > 1e-12 ? 1 : (dirX < -1e-12 ? -1 : 0);
        int stepY = dirY > 1e-12 ? 1 : (dirY < -1e-12 ? -1 : 0);
        int stepZ = dirZ > 1e-12 ? 1 : (dirZ < -1e-12 ? -1 : 0);

        double tDeltaX = stepX != 0 ? Math.Abs(vx / dirX) : double.PositiveInfinity;
        double tDeltaY = stepY != 0 ? Math.Abs(vy / dirY) : double.PositiveInfinity;
        double tDeltaZ = stepZ != 0 ? Math.Abs(vz / dirZ) : double.PositiveInfinity;

        double tMaxX = stepX > 0 ? (((grid.Origin.X + curX + 1) * vx) - localRay.Origin.X) / dirX
                     : stepX < 0 ? (((grid.Origin.X + curX) * vx) - localRay.Origin.X) / dirX
                     : double.PositiveInfinity;

        double tMaxY = stepY > 0 ? (((grid.Origin.Y + curY + 1) * vy) - localRay.Origin.Y) / dirY
                     : stepY < 0 ? (((grid.Origin.Y + curY) * vy) - localRay.Origin.Y) / dirY
                     : double.PositiveInfinity;

        double tMaxZ = stepZ > 0 ? (((grid.Origin.Z + curZ + 1) * vz) - localRay.Origin.Z) / dirZ
                     : stepZ < 0 ? (((grid.Origin.Z + curZ) * vz) - localRay.Origin.Z) / dirZ
                     : double.PositiveInfinity;

        int maxSteps = (int)(Math.Max(1, grid.Bounds.Width) + Math.Max(1, grid.Bounds.Height) + Math.Max(1, grid.Bounds.Depth)) * 3 + 100;

        for (int step = 0; step < maxSteps; step++)
        {
            if (curX < grid.Bounds.Min.X || curX > grid.Bounds.Max.X ||
                curY < grid.Bounds.Min.Y || curY > grid.Bounds.Max.Y ||
                curZ < grid.Bounds.Min.Z || curZ > grid.Bounds.Max.Z)
            {
                break;
            }

            var pos = new VoxelPosition(curX, curY, curZ);
            if (grid.TryGetVoxel(pos, out var voxel) && voxel is not null && !voxel.IsAir)
            {
                double bMinX = (grid.Origin.X + pos.X) * vx;
                double bMinY = (grid.Origin.Y + pos.Y) * vy;
                double bMinZ = (grid.Origin.Z + pos.Z) * vz;
                var box = new BoundingBox(new Vector3D(bMinX, bMinY, bMinZ), new Vector3D(bMinX + vx, bMinY + vy, bMinZ + vz));

                if (localRay.Intersects(box, out double localDist))
                {
                    var localHitPoint = localRay.GetPoint(localDist);
                    var localNormal = CalculateBoxNormal(box, localHitPoint);
                    var worldHitPoint = worldMatrix.TransformPoint(localHitPoint);
                    var worldNormal = worldMatrix.TransformNormal(localNormal).Normalize();
                    double worldDist = (worldHitPoint - worldRay.Origin).Length();

                    string blockId = voxel.State?.GetFullIdentifier() ??
                        (grid.Palette.TryGet(voxel.PaletteIndex, out var pEntry) ? pEntry?.State.GetFullIdentifier() : null) ??
                        "minecraft:stone";

                    string blockState = voxel.State?.GetCanonicalStateKey() ??
                        (grid.Palette.TryGet(voxel.PaletteIndex, out var pEntry2) ? pEntry2?.State.GetCanonicalStateKey() : null) ??
                        blockId;

                    return new RaycastHitResult(
                        node.Id,
                        null,
                        worldDist,
                        worldHitPoint,
                        worldNormal,
                        0,
                        pos,
                        blockId,
                        blockState);
                }
            }

            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    curX += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    curZ += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
            else
            {
                if (tMaxY < tMaxZ)
                {
                    curY += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    curZ += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
        }

        // Fallback: Precise voxel bounding box check across all occupied voxels
        Voxel? closestVoxel = null;
        double closestLocalDist = double.MaxValue;
        BoundingBox closestBox = default;

        foreach (var voxel in grid.Voxels)
        {
            if (voxel.IsAir) continue;

            double bMinX = (grid.Origin.X + voxel.Position.X) * vx;
            double bMinY = (grid.Origin.Y + voxel.Position.Y) * vy;
            double bMinZ = (grid.Origin.Z + voxel.Position.Z) * vz;
            var box = new BoundingBox(new Vector3D(bMinX, bMinY, bMinZ), new Vector3D(bMinX + vx, bMinY + vy, bMinZ + vz));

            if (localRay.Intersects(box, out double localDist) && localDist < closestLocalDist)
            {
                closestLocalDist = localDist;
                closestVoxel = voxel;
                closestBox = box;
            }
        }

        if (closestVoxel is not null)
        {
            var localHitPoint = localRay.GetPoint(closestLocalDist);
            var localNormal = CalculateBoxNormal(closestBox, localHitPoint);
            var worldHitPoint = worldMatrix.TransformPoint(localHitPoint);
            var worldNormal = worldMatrix.TransformNormal(localNormal).Normalize();
            double worldDist = (worldHitPoint - worldRay.Origin).Length();

            string blockId = closestVoxel.State?.GetFullIdentifier() ??
                (grid.Palette.TryGet(closestVoxel.PaletteIndex, out var pEntry) ? pEntry?.State.GetFullIdentifier() : null) ??
                "minecraft:stone";

            string blockState = closestVoxel.State?.GetCanonicalStateKey() ??
                (grid.Palette.TryGet(closestVoxel.PaletteIndex, out var pEntry2) ? pEntry2?.State.GetCanonicalStateKey() : null) ??
                blockId;

            return new RaycastHitResult(
                node.Id,
                null,
                worldDist,
                worldHitPoint,
                worldNormal,
                0,
                closestVoxel.Position,
                blockId,
                blockState);
        }

        return null;
    }

    private static Vector3D CalculateBoxNormal(BoundingBox box, Vector3D hitPoint)
    {
        double dMinX = Math.Abs(hitPoint.X - box.Min.X);
        double dMaxX = Math.Abs(hitPoint.X - box.Max.X);
        double dMinY = Math.Abs(hitPoint.Y - box.Min.Y);
        double dMaxY = Math.Abs(hitPoint.Y - box.Max.Y);
        double dMinZ = Math.Abs(hitPoint.Z - box.Min.Z);
        double dMaxZ = Math.Abs(hitPoint.Z - box.Max.Z);

        double minDist = dMinX;
        Vector3D normal = new(-1, 0, 0);

        if (dMaxX < minDist) { minDist = dMaxX; normal = new(1, 0, 0); }
        if (dMinY < minDist) { minDist = dMinY; normal = new(0, -1, 0); }
        if (dMaxY < minDist) { minDist = dMaxY; normal = new(0, 1, 0); }
        if (dMinZ < minDist) { minDist = dMinZ; normal = new(0, 0, -1); }
        if (dMaxZ < minDist) { normal = new(0, 0, 1); }

        return normal;
    }

    private static bool IntersectBox(Ray3D ray, BoundingBox box, out double tNear, out double tFar)
    {
        tNear = 0.0;
        tFar = double.MaxValue;

        if (!box.IsValid)
        {
            return false;
        }

        if (!IntersectSlab(ray.Origin.X, ray.Direction.X, box.Min.X, box.Max.X, ref tNear, ref tFar) ||
            !IntersectSlab(ray.Origin.Y, ray.Direction.Y, box.Min.Y, box.Max.Y, ref tNear, ref tFar) ||
            !IntersectSlab(ray.Origin.Z, ray.Direction.Z, box.Min.Z, box.Max.Z, ref tNear, ref tFar))
        {
            return false;
        }

        return tFar >= Math.Max(0.0, tNear);
    }

    private static bool IntersectSlab(double origin, double dir, double min, double max, ref double tMin, ref double tMax)
    {
        if (Math.Abs(dir) <= MathUtilities.Epsilon)
        {
            return origin >= min && origin <= max;
        }

        double invDir = 1.0 / dir;
        double t1 = (min - origin) * invDir;
        double t2 = (max - origin) * invDir;

        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);

        return tMin <= tMax;
    }

    /// <summary>
    /// Möller–Trumbore ray-triangle intersection algorithm.
    /// </summary>
    public static bool IntersectRayTriangle(
        Ray3D ray,
        Vector3D v0,
        Vector3D v1,
        Vector3D v2,
        out double distance,
        out Vector3D normal)
    {
        distance = 0.0;
        normal = Vector3D.UnitY;

        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var pvec = Vector3D.Cross(ray.Direction, edge2);
        double det = Vector3D.Dot(edge1, pvec);

        if (Math.Abs(det) <= MathUtilities.Epsilon)
        {
            return false;
        }

        double invDet = 1.0 / det;
        var tvec = ray.Origin - v0;
        double u = Vector3D.Dot(tvec, pvec) * invDet;

        if (u < 0.0 || u > 1.0)
        {
            return false;
        }

        var qvec = Vector3D.Cross(tvec, edge1);
        double v = Vector3D.Dot(ray.Direction, qvec) * invDet;

        if (v < 0.0 || u + v > 1.0)
        {
            return false;
        }

        double t = Vector3D.Dot(edge2, qvec) * invDet;
        if (t <= MathUtilities.Epsilon)
        {
            return false;
        }

        distance = t;
        normal = Vector3D.Cross(edge1, edge2).Normalize();
        if (det < 0.0) normal = -normal;
        return true;
    }
}

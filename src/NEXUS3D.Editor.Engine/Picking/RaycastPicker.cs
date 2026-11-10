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

            if (localRay.Intersects(aabb, out double localDist))
            {
                var localHitPoint = localRay.GetPoint(localDist);
                var worldHitPoint = worldMatrix.TransformPoint(localHitPoint);
                double worldDist = (worldHitPoint - worldRay.Origin).Length();

                hits.Add(new RaycastHitResult(
                    node.Id,
                    null,
                    worldDist,
                    worldHitPoint,
                    new Vector3D(0, 1, 0),
                    0));
            }
        }
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

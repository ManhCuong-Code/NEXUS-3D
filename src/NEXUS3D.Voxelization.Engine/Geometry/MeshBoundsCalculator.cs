using System.Numerics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;

namespace NEXUS3D.Voxelization.Engine.Geometry;

/// <summary>
/// Computes bounding boxes and extents for meshes and voxel transformation grids.
/// </summary>
public static class MeshBoundsCalculator
{
    public static (Vector3 Min, Vector3 Max) CalculateMeshBounds(MeshData mesh, Matrix4x4? transform = null)
    {
        if (mesh.Vertices.Count == 0)
        {
            return (Vector3.Zero, Vector3.Zero);
        }

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var mat = transform ?? Matrix4x4.Identity;

        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            var v = mesh.Vertices[i].Position;
            var pos = new Vector3((float)v.X, (float)v.Y, (float)v.Z);
            if (mat != Matrix4x4.Identity)
            {
                pos = Vector3.Transform(pos, mat);
            }

            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
        }

        return (min, max);
    }

    public static (Vector3 Min, Vector3 Max) Encapsulate(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
    {
        return (Vector3.Min(aMin, bMin), Vector3.Max(aMax, bMax));
    }
}

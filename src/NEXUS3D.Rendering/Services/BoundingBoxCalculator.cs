using System.Numerics;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Computes tight bounding boxes for meshes, scene nodes, and entire render scenes.
/// </summary>
public sealed class BoundingBoxCalculator : IBoundingBoxCalculator
{
    public (Vector3 Min, Vector3 Max) CalculateMeshBounds(Vector3[] positions, Matrix4x4 transform)
    {
        if (positions.Length == 0)
        {
            return (Vector3.Zero, Vector3.Zero);
        }

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < positions.Length; i++)
        {
            var p = Vector3.Transform(positions[i], transform);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return (min, max);
    }

    public (Vector3 Min, Vector3 Max) CalculateNodeBounds(RenderNode node)
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool hasData = false;

        foreach (var mesh in node.Meshes)
        {
            if (mesh.Positions.Length > 0)
            {
                var (mMin, mMax) = CalculateMeshBounds(mesh.Positions, node.WorldTransform);
                min = Vector3.Min(min, mMin);
                max = Vector3.Max(max, mMax);
                hasData = true;
            }
        }

        foreach (var chunk in node.VoxelChunks)
        {
            if (chunk.Mesh is not null && chunk.Mesh.Positions.Length > 0)
            {
                var (cMin, cMax) = CalculateMeshBounds(chunk.Mesh.Positions, node.WorldTransform);
                min = Vector3.Min(min, cMin);
                max = Vector3.Max(max, cMax);
                hasData = true;
            }
        }

        foreach (var child in node.Children)
        {
            var (cMin, cMax) = CalculateNodeBounds(child);
            if (cMin != Vector3.Zero || cMax != Vector3.Zero)
            {
                min = Vector3.Min(min, cMin);
                max = Vector3.Max(max, cMax);
                hasData = true;
            }
        }

        if (!hasData)
        {
            return (Vector3.Zero, Vector3.Zero);
        }

        node.BoundsMin = min;
        node.BoundsMax = max;
        return (min, max);
    }

    public (Vector3 Min, Vector3 Max) CalculateSceneBounds(RenderScene scene)
    {
        if (scene.RootNodes.Count == 0)
        {
            return (new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
        }

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool hasData = false;

        foreach (var node in scene.RootNodes)
        {
            var (nMin, nMax) = CalculateNodeBounds(node);
            if (nMin != Vector3.Zero || nMax != Vector3.Zero)
            {
                min = Vector3.Min(min, nMin);
                max = Vector3.Max(max, nMax);
                hasData = true;
            }
        }

        if (!hasData)
        {
            min = new Vector3(-1, -1, -1);
            max = new Vector3(1, 1, 1);
        }

        scene.BoundsMin = min;
        scene.BoundsMax = max;
        return (min, max);
    }
}

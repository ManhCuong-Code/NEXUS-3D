using System.Collections.Concurrent;
using System.IO;
using System.Numerics;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Voxelization.Engine.Processing;

/// <summary>
/// Computes barycentric projections and samples colors from materials, diffuse textures, and vertex colors.
/// </summary>
public sealed class VoxelColorSampler
{
    private readonly ConcurrentDictionary<string, byte[]?> _textureByteCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Computes barycentric coordinates (u, v, w) of point p projected onto triangle (a, b, c).
    /// </summary>
    public static Vector3 ComputeBarycentric(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
    {
        var v0 = b - a;
        var v1 = c - a;
        var v2 = p - a;

        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);

        float denom = d00 * d11 - d01 * d01;
        if (MathF.Abs(denom) < 1e-8f)
        {
            return new Vector3(1f / 3f, 1f / 3f, 1f / 3f);
        }

        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;

        // Clamp to triangle boundary
        u = Math.Clamp(u, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);
        w = Math.Clamp(w, 0f, 1f);
        float sum = u + v + w;
        return sum > 0f ? new Vector3(u / sum, v / sum, w / sum) : new Vector3(1f / 3f, 1f / 3f, 1f / 3f);
    }

    /// <summary>
    /// Interpolates vertex color across barycentric weights.
    /// </summary>
    public static Vector4 InterpolateColor(Vector4 c0, Vector4 c1, Vector4 c2, Vector3 barycentric)
    {
        return c0 * barycentric.X + c1 * barycentric.Y + c2 * barycentric.Z;
    }

    /// <summary>
    /// Interpolates UV coordinates across barycentric weights.
    /// </summary>
    public static Vector2 InterpolateUV(Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector3 barycentric)
    {
        return uv0 * barycentric.X + uv1 * barycentric.Y + uv2 * barycentric.Z;
    }

    /// <summary>
    /// Resolves the dominant color for a voxel from its intersected triangle.
    /// </summary>
    public Vector4 SampleVoxelColor(
        WorkingTriangle triangle,
        Vector3 voxelCenter,
        Scene3D? scene,
        bool sampleTexture,
        bool sampleVertexColor)
    {
        var bary = ComputeBarycentric(triangle.V0, triangle.V1, triangle.V2, voxelCenter);

        // 1. Try vertex color if enabled and not plain white
        if (sampleVertexColor)
        {
            var vertCol = InterpolateColor(triangle.Color0, triangle.Color1, triangle.Color2, bary);
            if (vertCol != Vector4.One && vertCol.W > 0.05f)
            {
                return vertCol;
            }
        }

        // 2. Try material diffuse color
        if (scene is not null && triangle.MaterialId.HasValue)
        {
            var mat = scene.Materials.FindById(triangle.MaterialId.Value);
            if (mat is not null)
            {
                return new Vector4(
                    (float)mat.BaseColor.R,
                    (float)mat.BaseColor.G,
                    (float)mat.BaseColor.B,
                    (float)mat.Opacity);
            }
        }

        // Fallback default white
        return Vector4.One;
    }
}

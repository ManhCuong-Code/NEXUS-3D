using System.Numerics;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Generates line geometry representing vertex normal vectors for debugging and inspection.
/// </summary>
public sealed class NormalVisualizer
{
    public RenderMesh GenerateNormalLines(RenderMesh mesh, float normalLength = 0.5f, Vector4? normalColor = null)
    {
        if (mesh is null || mesh.Positions.Length == 0 || mesh.Normals.Length == 0)
        {
            return new RenderMesh { Name = "EmptyNormals" };
        }

        int count = mesh.Positions.Length;
        var positions = new List<Vector3>(count * 2);
        var indices = new List<int>(count * 2);
        var colors = new List<Vector4>(count * 2);
        var color = normalColor ?? new Vector4(0f, 1f, 1f, 1f); // Cyan

        for (int i = 0; i < count; i++)
        {
            var p0 = mesh.Positions[i];
            var n = mesh.Normals[i];
            var p1 = p0 + n * normalLength;

            int idx = positions.Count;
            positions.Add(p0);
            positions.Add(p1);

            colors.Add(color);
            colors.Add(color);

            indices.Add(idx);
            indices.Add(idx + 1);
        }

        return new RenderMesh
        {
            Name = $"{mesh.Name}_Normals",
            Positions = [.. positions],
            Colors = [.. colors],
            WireframeIndices = [.. indices],
            BoundsMin = mesh.BoundsMin,
            BoundsMax = mesh.BoundsMax
        };
    }
}

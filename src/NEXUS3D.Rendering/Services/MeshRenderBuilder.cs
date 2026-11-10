using System.Numerics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Prepares GPU-friendly RenderMesh from USM MeshData, performing triangulation and wireframe generation.
/// </summary>
public sealed class MeshRenderBuilder
{
    public RenderMesh Build(MeshData mesh, string? fallbackMaterialId = null)
    {
        if (mesh is null || mesh.Vertices.Count == 0)
        {
            return new RenderMesh
            {
                Name = mesh?.Name ?? "EmptyMesh",
                MaterialId = fallbackMaterialId
            };
        }

        int vertexCount = mesh.Vertices.Count;
        var positions = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var colors = new Vector4[vertexCount];

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool hasNormals = false;

        for (int i = 0; i < vertexCount; i++)
        {
            var v = mesh.Vertices[i];
            var p = new Vector3((float)v.Position.X, (float)v.Position.Y, (float)v.Position.Z);
            positions[i] = p;

            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);

            if (v.Normal.HasValue)
            {
                normals[i] = new Vector3((float)v.Normal.Value.X, (float)v.Normal.Value.Y, (float)v.Normal.Value.Z);
                hasNormals = true;
            }

            if (v.TexCoord.HasValue)
            {
                uvs[i] = new Vector2((float)v.TexCoord.Value.X, (float)v.TexCoord.Value.Y);
            }

            if (v.Color.HasValue)
            {
                colors[i] = new Vector4((float)v.Color.Value.X, (float)v.Color.Value.Y, (float)v.Color.Value.Z, (float)v.Color.Value.W);
            }
            else
            {
                colors[i] = Vector4.One;
            }
        }

        var triangleIndices = new List<int>(mesh.Faces.Count * 3);
        var wireframeIndices = new List<int>(mesh.Faces.Count * 6);

        foreach (var face in mesh.Faces)
        {
            int count = face.VertexIndices.Count;
            if (count < 3)
            {
                continue;
            }

            // Wireframe edges
            for (int i = 0; i < count; i++)
            {
                wireframeIndices.Add(face.VertexIndices[i]);
                wireframeIndices.Add(face.VertexIndices[(i + 1) % count]);
            }

            // Triangulate
            if (count == 3)
            {
                triangleIndices.Add(face.VertexIndices[0]);
                triangleIndices.Add(face.VertexIndices[1]);
                triangleIndices.Add(face.VertexIndices[2]);
            }
            else if (count == 4)
            {
                // Quad split: (0, 1, 2) and (0, 2, 3)
                triangleIndices.Add(face.VertexIndices[0]);
                triangleIndices.Add(face.VertexIndices[1]);
                triangleIndices.Add(face.VertexIndices[2]);

                triangleIndices.Add(face.VertexIndices[0]);
                triangleIndices.Add(face.VertexIndices[2]);
                triangleIndices.Add(face.VertexIndices[3]);
            }
            else
            {
                // Triangle fan for n-gons
                for (int i = 1; i < count - 1; i++)
                {
                    triangleIndices.Add(face.VertexIndices[0]);
                    triangleIndices.Add(face.VertexIndices[i]);
                    triangleIndices.Add(face.VertexIndices[i + 1]);
                }
            }
        }

        // Auto-generate normals if missing
        if (!hasNormals && triangleIndices.Count >= 3)
        {
            normals = GenerateFlatNormals(positions, triangleIndices);
        }

        string? matId = fallbackMaterialId;
        if (mesh.Submeshes.Count > 0 && mesh.Submeshes[0].MaterialId is { } subMatId)
        {
            matId = subMatId.ToString();
        }

        return new RenderMesh
        {
            Id = mesh.Id.ToString(),
            Name = mesh.Name,
            Positions = positions,
            Normals = normals,
            UVs = uvs,
            Colors = colors,
            Indices = [.. triangleIndices],
            WireframeIndices = [.. wireframeIndices],
            MaterialId = matId,
            BoundsMin = min == new Vector3(float.MaxValue) ? Vector3.Zero : min,
            BoundsMax = max == new Vector3(float.MinValue) ? Vector3.Zero : max
        };
    }

    private static Vector3[] GenerateFlatNormals(Vector3[] positions, List<int> indices)
    {
        var normals = new Vector3[positions.Length];

        for (int i = 0; i < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if (i0 < positions.Length && i1 < positions.Length && i2 < positions.Length)
            {
                var v0 = positions[i0];
                var v1 = positions[i1];
                var v2 = positions[i2];

                var n = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
                if (!float.IsNaN(n.X))
                {
                    normals[i0] += n;
                    normals[i1] += n;
                    normals[i2] += n;
                }
            }
        }

        for (int i = 0; i < normals.Length; i++)
        {
            var len = normals[i].Length();
            normals[i] = len > 0.0001f ? normals[i] / len : Vector3.UnitY;
        }

        return normals;
    }
}

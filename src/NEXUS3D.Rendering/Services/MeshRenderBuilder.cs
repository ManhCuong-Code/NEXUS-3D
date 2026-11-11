using System.Numerics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Prepares GPU-friendly RenderMesh from USM MeshData, supporting multi-material submesh partitioning,
/// vertex color preservation, triangulation, and wireframe generation.
/// </summary>
public sealed class MeshRenderBuilder
{
    public List<RenderMesh> BuildMulti(MeshData mesh, string? fallbackMaterialId = null)
    {
        if (mesh is null || mesh.Vertices.Count == 0)
        {
            return [new RenderMesh { Name = mesh?.Name ?? "EmptyMesh", MaterialId = fallbackMaterialId }];
        }

        if (mesh.Submeshes.Count <= 1)
        {
            return [Build(mesh, fallbackMaterialId)];
        }

        var results = new List<RenderMesh>(mesh.Submeshes.Count);
        for (int sIdx = 0; sIdx < mesh.Submeshes.Count; sIdx++)
        {
            var submesh = mesh.Submeshes[sIdx];
            int start = submesh.FaceStartIndex;
            int count = submesh.FaceCount;

            if (start < 0 || start >= mesh.Faces.Count || count <= 0)
            {
                continue;
            }

            int end = Math.Min(start + count, mesh.Faces.Count);
            var subFaces = new List<Face>(end - start);
            for (int f = start; f < end; f++)
            {
                subFaces.Add(mesh.Faces[f]);
            }

            string matId = submesh.MaterialId?.ToString() ?? fallbackMaterialId ?? "default";
            string name = $"{mesh.Name}_{submesh.Name}";

            var rMesh = BuildFromFaces(mesh, subFaces, name, matId);
            if (rMesh.TriangleCount > 0)
            {
                results.Add(rMesh);
            }
        }

        return results.Count > 0 ? results : [Build(mesh, fallbackMaterialId)];
    }

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

        string? matId = fallbackMaterialId;
        if (mesh.Submeshes.Count > 0 && mesh.Submeshes[0].MaterialId is { } subMatId)
        {
            matId = subMatId.ToString();
        }

        return BuildFromFaces(mesh, mesh.Faces, mesh.Name, matId);
    }

    private static RenderMesh BuildFromFaces(MeshData mesh, IReadOnlyList<Face> faces, string name, string? materialId)
    {
        var globalToLocal = new Dictionary<int, int>();
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Vector4>();

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool hasNormals = false;

        int GetOrAddVertex(int globalIdx)
        {
            if (globalToLocal.TryGetValue(globalIdx, out int localIdx))
            {
                return localIdx;
            }

            localIdx = positions.Count;
            globalToLocal[globalIdx] = localIdx;

            if (globalIdx >= 0 && globalIdx < mesh.Vertices.Count)
            {
                var v = mesh.Vertices[globalIdx];
                var p = new Vector3((float)v.Position.X, (float)v.Position.Y, (float)v.Position.Z);
                positions.Add(p);

                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);

                if (v.Normal.HasValue)
                {
                    normals.Add(new Vector3((float)v.Normal.Value.X, (float)v.Normal.Value.Y, (float)v.Normal.Value.Z));
                    hasNormals = true;
                }
                else
                {
                    normals.Add(Vector3.UnitY);
                }

                if (v.TexCoord.HasValue)
                {
                    uvs.Add(new Vector2((float)v.TexCoord.Value.X, (float)v.TexCoord.Value.Y));
                }
                else
                {
                    uvs.Add(Vector2.Zero);
                }

                if (v.Color.HasValue)
                {
                    colors.Add(new Vector4((float)v.Color.Value.X, (float)v.Color.Value.Y, (float)v.Color.Value.Z, (float)v.Color.Value.W));
                }
                else
                {
                    colors.Add(Vector4.One);
                }
            }
            else
            {
                positions.Add(Vector3.Zero);
                normals.Add(Vector3.UnitY);
                uvs.Add(Vector2.Zero);
                colors.Add(Vector4.One);
            }

            return localIdx;
        }

        var triangleIndices = new List<int>(faces.Count * 3);
        var wireframeIndices = new List<int>(faces.Count * 6);

        foreach (var face in faces)
        {
            int count = face.VertexIndices.Count;
            if (count < 3)
            {
                continue;
            }

            // Remap vertex indices to local submesh buffers
            var localVerts = new int[count];
            for (int i = 0; i < count; i++)
            {
                localVerts[i] = GetOrAddVertex(face.VertexIndices[i]);
            }

            // Wireframe edges
            for (int i = 0; i < count; i++)
            {
                wireframeIndices.Add(localVerts[i]);
                wireframeIndices.Add(localVerts[(i + 1) % count]);
            }

            // Triangulate
            if (count == 3)
            {
                triangleIndices.Add(localVerts[0]);
                triangleIndices.Add(localVerts[1]);
                triangleIndices.Add(localVerts[2]);
            }
            else if (count == 4)
            {
                triangleIndices.Add(localVerts[0]);
                triangleIndices.Add(localVerts[1]);
                triangleIndices.Add(localVerts[2]);

                triangleIndices.Add(localVerts[0]);
                triangleIndices.Add(localVerts[2]);
                triangleIndices.Add(localVerts[3]);
            }
            else
            {
                // Triangle fan for n-gons
                for (int i = 1; i < count - 1; i++)
                {
                    triangleIndices.Add(localVerts[0]);
                    triangleIndices.Add(localVerts[i]);
                    triangleIndices.Add(localVerts[i + 1]);
                }
            }
        }

        var posArray = positions.ToArray();
        var normArray = normals.ToArray();

        // Auto-generate normals if missing
        if (!hasNormals && triangleIndices.Count >= 3)
        {
            normArray = GenerateFlatNormals(posArray, triangleIndices);
        }

        return new RenderMesh
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Positions = posArray,
            Normals = normArray,
            UVs = uvs.ToArray(),
            Colors = colors.ToArray(),
            Indices = triangleIndices.ToArray(),
            WireframeIndices = wireframeIndices.ToArray(),
            MaterialId = materialId,
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

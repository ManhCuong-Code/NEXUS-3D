using System.Numerics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Voxelization.Abstractions.Models;

namespace NEXUS3D.Voxelization.Engine.Processing;

/// <summary>
/// A preprocessed triangle ready for high-performance volumetric rasterization.
/// </summary>
public sealed record WorkingTriangle(
    Vector3 V0,
    Vector3 V1,
    Vector3 V2,
    Vector3 Normal,
    Vector2 UV0,
    Vector2 UV1,
    Vector2 UV2,
    Vector4 Color0,
    Vector4 Color1,
    Vector4 Color2,
    Guid? MaterialId,
    int OriginalFaceIndex)
{
    public float Area => Vector3.Cross(V1 - V0, V2 - V0).Length() * 0.5f;
    public Vector3 Center => (V0 + V1 + V2) / 3f;
    public (Vector3 Min, Vector3 Max) Bounds =>
        (Vector3.Min(V0, Vector3.Min(V1, V2)), Vector3.Max(V0, Vector3.Max(V1, V2)));
}

public sealed class MeshPreprocessResult
{
    public IReadOnlyList<WorkingTriangle> Triangles { get; init; } = [];
    public bool IsClosedManifold { get; init; }
    public Vector3 BoundsMin { get; init; }
    public Vector3 BoundsMax { get; init; }
    public IReadOnlyList<VoxelizationWarning> Warnings { get; init; } = [];
}

/// <summary>
/// Preprocesses, validates, triangulates, and checks topological manifoldness of input meshes.
/// </summary>
public sealed class MeshPreprocessor
{
    public MeshPreprocessResult Preprocess(MeshData mesh, Matrix4x4? worldTransform = null)
    {
        var warnings = new List<VoxelizationWarning>();
        if (mesh is null || mesh.Vertices.Count == 0 || mesh.Faces.Count == 0)
        {
            return new MeshPreprocessResult
            {
                BoundsMin = Vector3.Zero,
                BoundsMax = Vector3.Zero,
                IsClosedManifold = false,
                Warnings = warnings
            };
        }

        var mat = worldTransform ?? Matrix4x4.Identity;
        int vertexCount = mesh.Vertices.Count;

        // 1. Validate vertices for NaN/Infinity
        var positions = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var colors = new Vector4[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            var v = mesh.Vertices[i];
            if (!double.IsFinite(v.Position.X) || !double.IsFinite(v.Position.Y) || !double.IsFinite(v.Position.Z))
            {
                positions[i] = Vector3.Zero;
                warnings.Add(new VoxelizationWarning("VOX_INVALID_VERTEX", $"Vertex #{i} contained non-finite numbers and was reset to origin.", VoxelizationWarningSeverity.Warning));
            }
            else
            {
                var p = new Vector3((float)v.Position.X, (float)v.Position.Y, (float)v.Position.Z);
                positions[i] = mat != Matrix4x4.Identity ? Vector3.Transform(p, mat) : p;
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

        // 2. Triangulate and collect faces
        var triangles = new List<WorkingTriangle>(mesh.Faces.Count * 2);
        var edgeOccurrences = new Dictionary<(int A, int B), int>();
        int degenerateCount = 0;

        var bMin = new Vector3(float.MaxValue);
        var bMax = new Vector3(float.MinValue);

        for (int f = 0; f < mesh.Faces.Count; f++)
        {
            var face = mesh.Faces[f];
            var indices = face.VertexIndices;
            if (indices == null || indices.Count < 3) continue;

            // Subdivide polygon into triangles (Fan)
            for (int i = 1; i < indices.Count - 1; i++)
            {
                int i0 = indices[0];
                int i1 = indices[i];
                int i2 = indices[i + 1];

                if (i0 < 0 || i0 >= vertexCount || i1 < 0 || i1 >= vertexCount || i2 < 0 || i2 >= vertexCount)
                {
                    continue; // Skip out-of-range index
                }

                var v0 = positions[i0];
                var v1 = positions[i1];
                var v2 = positions[i2];

                var e0 = v1 - v0;
                var e1 = v2 - v0;
                var cross = Vector3.Cross(e0, e1);
                float area = cross.Length() * 0.5f;

                if (area < 1e-8f)
                {
                    degenerateCount++;
                    continue; // Skip degenerate triangle
                }

                var normal = Vector3.Normalize(cross);

                // Update bounding box
                bMin = Vector3.Min(bMin, Vector3.Min(v0, Vector3.Min(v1, v2)));
                bMax = Vector3.Max(bMax, Vector3.Max(v0, Vector3.Max(v1, v2)));

                // Track directed edges for manifold check
                AddEdge(edgeOccurrences, i0, i1);
                AddEdge(edgeOccurrences, i1, i2);
                AddEdge(edgeOccurrences, i2, i0);

                triangles.Add(new WorkingTriangle(
                    v0, v1, v2, normal,
                    uvs[i0], uvs[i1], uvs[i2],
                    colors[i0], colors[i1], colors[i2],
                    face.MaterialId,
                    f));
            }
        }

        if (degenerateCount > 0)
        {
            warnings.Add(VoxelizationWarning.DegenerateTrianglesSkipped(degenerateCount, mesh.Name));
        }

        // 3. Manifold check: for a 2-manifold closed surface, every edge (a, b) must have an exact twin (b, a)
        bool isClosed = triangles.Count > 0;
        foreach (var (edge, count) in edgeOccurrences)
        {
            if (count > 1 || !edgeOccurrences.ContainsKey((edge.B, edge.A)))
            {
                isClosed = false;
                break;
            }
        }

        if (!isClosed && triangles.Count > 0)
        {
            warnings.Add(VoxelizationWarning.OpenMeshNonManifold(mesh.Name));
        }

        if (triangles.Count == 0)
        {
            bMin = Vector3.Zero;
            bMax = Vector3.Zero;
        }

        return new MeshPreprocessResult
        {
            Triangles = triangles,
            IsClosedManifold = isClosed,
            BoundsMin = bMin,
            BoundsMax = bMax,
            Warnings = warnings
        };
    }

    private static void AddEdge(Dictionary<(int A, int B), int> edges, int a, int b)
    {
        var key = (a, b);
        edges[key] = edges.GetValueOrDefault(key) + 1;
    }
}

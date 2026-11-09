using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;

namespace NEXUS3D.Domain.Validation;

/// <summary>
/// Validates <see cref="MeshData"/> geometry, topology, indices, normals, UVs, and degenerate faces.
/// Distinguishes structural errors from optimization warnings (such as duplicate vertex positions).
/// </summary>
public sealed class MeshValidator : IValidator<MeshData>
{
    public ValidationResult Validate(MeshData? mesh)
    {
        var result = new ValidationResult();
        if (mesh is null)
        {
            result.AddError(UsmErrorCodes.InvalidMesh, "MeshData instance cannot be null.", "Mesh");
            return result;
        }

        if (mesh.Id == Guid.Empty)
        {
            result.AddError(UsmErrorCodes.InvalidMesh, "Mesh ID cannot be Guid.Empty.", "Mesh.Id");
        }

        if (string.IsNullOrWhiteSpace(mesh.Name))
        {
            result.AddError(UsmErrorCodes.InvalidMesh, "Mesh name cannot be empty.", "Mesh.Name", mesh.Id);
        }

        int vertexCount = mesh.Vertices.Count;
        var seenPositions = new HashSet<Vector3D>();
        int duplicatePositionCount = 0;

        for (int i = 0; i < vertexCount; i++)
        {
            Vertex? v = mesh.Vertices[i];
            if (v is null)
            {
                result.AddError(UsmErrorCodes.InvalidMesh, $"Vertex at index {i} is null.", $"Mesh.Vertices[{i}]", mesh.Id);
                continue;
            }

            if (!v.Position.IsFinite())
            {
                result.AddError(UsmErrorCodes.InvalidMesh, $"Vertex[{i}] Position contains NaN or Infinity.", $"Mesh.Vertices[{i}].Position", mesh.Id);
            }
            else if (!seenPositions.Add(v.Position))
            {
                duplicatePositionCount++;
            }

            if (v.Normal.HasValue)
            {
                Vector3D n = v.Normal.Value;
                if (!n.IsFinite() || n.LengthSquared() <= MathUtilities.Epsilon)
                {
                    result.AddError(UsmErrorCodes.InvalidMesh, $"Vertex[{i}] Normal is zero or non-finite.", $"Mesh.Vertices[{i}].Normal", mesh.Id);
                }
                else if (Math.Abs(n.LengthSquared() - 1.0) > MathUtilities.LooseEpsilon)
                {
                    result.AddWarning(UsmErrorCodes.InvalidMesh, $"Vertex[{i}] Normal is not unit length.", $"Mesh.Vertices[{i}].Normal", mesh.Id);
                }
            }

            if (v.TexCoord.HasValue && !v.TexCoord.Value.IsFinite())
            {
                result.AddError(UsmErrorCodes.InvalidMesh, $"Vertex[{i}] TexCoord (UV) contains NaN or Infinity.", $"Mesh.Vertices[{i}].TexCoord", mesh.Id);
            }
        }

        if (duplicatePositionCount > 0)
        {
            // Duplicate vertex positions are valid when UV/Normal seams exist, so report as Warning rather than Error.
            result.AddWarning(
                UsmErrorCodes.InvalidMesh,
                $"Mesh '{mesh.Name}' contains {duplicatePositionCount} coincident vertex positions that may be weldable.",
                "Mesh.Vertices",
                mesh.Id);
        }

        int minIndicesPerFace = mesh.Topology switch
        {
            MeshTopology.Points => 1,
            MeshTopology.Lines => 2,
            MeshTopology.Triangles => 3,
            MeshTopology.Polygons => 3,
            _ => 3
        };

        for (int fIdx = 0; fIdx < mesh.Faces.Count; fIdx++)
        {
            Face? face = mesh.Faces[fIdx];
            if (face is null || face.VertexIndices is null || face.VertexIndices.Count == 0)
            {
                result.AddError(UsmErrorCodes.InvalidMesh, $"Face[{fIdx}] is empty.", $"Mesh.Faces[{fIdx}]", mesh.Id);
                continue;
            }

            if (face.VertexIndices.Count < minIndicesPerFace)
            {
                result.AddError(
                    UsmErrorCodes.InvalidMesh,
                    $"Face[{fIdx}] has {face.VertexIndices.Count} vertices, fewer than minimum {minIndicesPerFace} for topology {mesh.Topology}.",
                    $"Mesh.Faces[{fIdx}].VertexIndices",
                    mesh.Id);
                continue;
            }

            if (mesh.Topology == MeshTopology.Triangles && face.VertexIndices.Count != 3)
            {
                result.AddError(
                    UsmErrorCodes.InvalidMesh,
                    $"Face[{fIdx}] has {face.VertexIndices.Count} indices, expected 3 for Triangles topology.",
                    $"Mesh.Faces[{fIdx}].VertexIndices",
                    mesh.Id);
            }

            bool allIndicesInRange = true;
            for (int k = 0; k < face.VertexIndices.Count; k++)
            {
                int vIdx = face.VertexIndices[k];
                if (vIdx < 0 || vIdx >= vertexCount)
                {
                    allIndicesInRange = false;
                    result.AddError(
                        UsmErrorCodes.InvalidVertexIndex,
                        $"Face[{fIdx}] references out-of-range vertex index {vIdx} (VertexCount={vertexCount}).",
                        $"Mesh.Faces[{fIdx}].VertexIndices[{k}]",
                        mesh.Id);
                }
            }

            if (!allIndicesInRange)
            {
                continue;
            }

            if (face.HasDuplicateIndices())
            {
                result.AddError(
                    UsmErrorCodes.InvalidMesh,
                    $"Face[{fIdx}] is degenerate (contains duplicate vertex indices).",
                    $"Mesh.Faces[{fIdx}]",
                    mesh.Id);
                continue;
            }

            if (face.VertexIndices.Count >= 3)
            {
                Vector3D p0 = mesh.Vertices[face.VertexIndices[0]].Position;
                Vector3D p1 = mesh.Vertices[face.VertexIndices[1]].Position;
                Vector3D p2 = mesh.Vertices[face.VertexIndices[2]].Position;
                var tri = new Triangle(p0, p1, p2);
                if (tri.IsDegenerate)
                {
                    result.AddError(
                        UsmErrorCodes.InvalidMesh,
                        $"Face[{fIdx}] has zero geometric area (collinear vertices).",
                        $"Mesh.Faces[{fIdx}]",
                        mesh.Id);
                }
            }
        }

        for (int sIdx = 0; sIdx < mesh.Submeshes.Count; sIdx++)
        {
            MeshSubmesh sub = mesh.Submeshes[sIdx];
            if (sub.FaceStartIndex < 0 || sub.FaceCount < 0 || (sub.FaceStartIndex + sub.FaceCount) > mesh.Faces.Count)
            {
                result.AddError(
                    UsmErrorCodes.InvalidMesh,
                    $"Submesh '{sub.Name}' face range [{sub.FaceStartIndex}, +{sub.FaceCount}] exceeds Faces count ({mesh.Faces.Count}).",
                    $"Mesh.Submeshes[{sIdx}]",
                    mesh.Id);
            }
        }

        return result;
    }
}

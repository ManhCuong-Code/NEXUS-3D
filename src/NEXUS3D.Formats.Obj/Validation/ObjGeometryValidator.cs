using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Geometry;

namespace NEXUS3D.Formats.Obj.Validation;

/// <summary>
/// Validates geometric primitives (<c>v</c>, <c>vt</c>, <c>vn</c>, <c>vp</c>, and <c>f</c> polygons)
/// inside a Wavefront <see cref="ObjDocument"/>.
/// </summary>
public sealed class ObjGeometryValidator
{
    public ValidationResult Validate(ObjDocument document, bool strictMode = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<ValidationIssue>();

        // 1. Validate vertex positions
        for (int i = 0; i < document.Positions.Count; i++)
        {
            ObjVertex v = document.Positions[i];
            if (!v.IsFinite)
            {
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidVertex,
                    $"Vertex position at index {i} (line {v.SourceLineNumber}) contains non-finite coordinates or zero W: ({v.X}, {v.Y}, {v.Z}, {v.W}).",
                    ValidationSeverity.Error,
                    $"v[{i}]"));
            }
        }

        // 2. Validate texture coordinates
        for (int i = 0; i < document.TextureCoordinates.Count; i++)
        {
            ObjTextureCoordinate vt = document.TextureCoordinates[i];
            if (!vt.IsFinite)
            {
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidTexCoord,
                    $"Texture coordinate at index {i} (line {vt.SourceLineNumber}) is non-finite: ({vt.U}, {vt.V}, {vt.W}).",
                    ValidationSeverity.Error,
                    $"vt[{i}]"));
            }
        }

        // 3. Validate vertex normals
        for (int i = 0; i < document.Normals.Count; i++)
        {
            ObjNormal vn = document.Normals[i];
            if (!vn.IsFinite)
            {
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidNormal,
                    $"Vertex normal at index {i} (line {vn.SourceLineNumber}) contains non-finite components: ({vn.X}, {vn.Y}, {vn.Z}).",
                    ValidationSeverity.Error,
                    $"vn[{i}]"));
            }
            else if (vn.IsZero)
            {
                ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidNormal,
                    $"Vertex normal at index {i} (line {vn.SourceLineNumber}) has zero length.",
                    severity,
                    $"vn[{i}]"));
            }
        }

        // 4. Validate polygon faces
        int posCount = document.Positions.Count;
        for (int i = 0; i < document.Faces.Count; i++)
        {
            ObjFace face = document.Faces[i];
            if (face.Vertices.Count < 3)
            {
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidFace,
                    $"Face at index {i} (line {face.SourceLineNumber}) has fewer than 3 vertices ({face.Vertices.Count}).",
                    ValidationSeverity.Error,
                    $"f[{i}]"));
                continue;
            }

            bool allIndicesInBounds = true;
            bool hasDuplicatePos = false;
            for (int a = 0; a < face.Vertices.Count; a++)
            {
                int posA = face.Vertices[a].PositionIndex;
                if (posA < 0 || posA >= posCount)
                {
                    allIndicesInBounds = false;
                    break;
                }

                for (int b = a + 1; b < face.Vertices.Count; b++)
                {
                    if (posA == face.Vertices[b].PositionIndex)
                    {
                        hasDuplicatePos = true;
                        break;
                    }
                }
            }

            if (hasDuplicatePos)
            {
                ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.DegeneratePolygon,
                    $"Face at index {i} (line {face.SourceLineNumber}) references the same vertex position multiple times.",
                    severity,
                    $"f[{i}]"));
            }

            if (allIndicesInBounds)
            {
                Vector3D faceNormal = face.ComputeFaceNormal(document.Positions);
                if (faceNormal.LengthSquared() <= MathUtilities.Epsilon)
                {
                    ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
                    issues.Add(new ValidationIssue(
                        ObjErrorCodes.DegeneratePolygon,
                        $"Face at index {i} (line {face.SourceLineNumber}) is collinear or has zero geometric area.",
                        severity,
                        $"f[{i}]"));
                }
            }
        }

        return ValidationResult.Failure(issues);
    }
}

using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Geometry;
using NEXUS3D.Formats.Obj.Materials;

namespace NEXUS3D.Formats.Obj.Validation;

/// <summary>
/// Validates index bounds (<c>v</c>, <c>vt</c>, <c>vn</c>) and material references (<c>usemtl</c>)
/// inside a Wavefront <see cref="ObjDocument"/>.
/// </summary>
public sealed class ObjReferenceValidator
{
    public ValidationResult Validate(
        ObjDocument document,
        MtlDocument? mtlDocument = null,
        bool strictMode = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issues = new List<ValidationIssue>();
        int posCount = document.Positions.Count;
        int vtCount = document.TextureCoordinates.Count;
        int vnCount = document.Normals.Count;

        var knownMaterials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (mtlDocument is not null)
        {
            for (int i = 0; i < mtlDocument.Materials.Count; i++)
            {
                knownMaterials.Add(mtlDocument.Materials[i].Name);
            }
        }

        var missingMaterialsReported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < document.Faces.Count; i++)
        {
            ObjFace face = document.Faces[i];
            for (int vIdx = 0; vIdx < face.Vertices.Count; vIdx++)
            {
                ObjFaceVertex corner = face.Vertices[vIdx];
                if (corner.PositionIndex < 0 || corner.PositionIndex >= posCount)
                {
                    issues.Add(new ValidationIssue(
                        ObjErrorCodes.IndexOutOfRange,
                        $"Face {i} (line {face.SourceLineNumber}) vertex {vIdx} position index {corner.PositionIndex} is out of bounds (0..{posCount - 1}).",
                        ValidationSeverity.Error,
                        $"f[{i}]"));
                }

                if (corner.TextureCoordIndex.HasValue &&
                    (corner.TextureCoordIndex.Value < 0 || corner.TextureCoordIndex.Value >= vtCount))
                {
                    issues.Add(new ValidationIssue(
                        ObjErrorCodes.IndexOutOfRange,
                        $"Face {i} (line {face.SourceLineNumber}) vertex {vIdx} UV index {corner.TextureCoordIndex.Value} is out of bounds (0..{vtCount - 1}).",
                        ValidationSeverity.Error,
                        $"f[{i}]"));
                }

                if (corner.NormalIndex.HasValue &&
                    (corner.NormalIndex.Value < 0 || corner.NormalIndex.Value >= vnCount))
                {
                    issues.Add(new ValidationIssue(
                        ObjErrorCodes.IndexOutOfRange,
                        $"Face {i} (line {face.SourceLineNumber}) vertex {vIdx} normal index {corner.NormalIndex.Value} is out of bounds (0..{vnCount - 1}).",
                        ValidationSeverity.Error,
                        $"f[{i}]"));
                }
            }

            if (mtlDocument is not null &&
                !string.IsNullOrWhiteSpace(face.MaterialName) &&
                !knownMaterials.Contains(face.MaterialName) &&
                missingMaterialsReported.Add(face.MaterialName))
            {
                ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.MissingMaterial,
                    $"Material '{face.MaterialName}' referenced by 'usemtl' (line {face.SourceLineNumber}) was not found in the loaded MTL library.",
                    severity,
                    $"usemtl:{face.MaterialName}"));
            }
        }

        for (int i = 0; i < document.Polylines.Count; i++)
        {
            ObjPolyline line = document.Polylines[i];
            for (int vIdx = 0; vIdx < line.Vertices.Count; vIdx++)
            {
                ObjPolylineVertex v = line.Vertices[vIdx];
                if (v.PositionIndex < 0 || v.PositionIndex >= posCount)
                {
                    issues.Add(new ValidationIssue(
                        ObjErrorCodes.IndexOutOfRange,
                        $"Polyline {i} (line {line.SourceLineNumber}) vertex {vIdx} position index {v.PositionIndex} is out of bounds.",
                        ValidationSeverity.Error,
                        $"l[{i}]"));
                }

                if (v.TextureCoordIndex.HasValue &&
                    (v.TextureCoordIndex.Value < 0 || v.TextureCoordIndex.Value >= vtCount))
                {
                    issues.Add(new ValidationIssue(
                        ObjErrorCodes.IndexOutOfRange,
                        $"Polyline {i} (line {line.SourceLineNumber}) vertex {vIdx} UV index {v.TextureCoordIndex.Value} is out of bounds.",
                        ValidationSeverity.Error,
                        $"l[{i}]"));
                }
            }
        }

        for (int i = 0; i < document.Points.Count; i++)
        {
            ObjPointElement pt = document.Points[i];
            for (int vIdx = 0; vIdx < pt.PositionIndices.Count; vIdx++)
            {
                int posIdx = pt.PositionIndices[vIdx];
                if (posIdx < 0 || posIdx >= posCount)
                {
                    issues.Add(new ValidationIssue(
                        ObjErrorCodes.IndexOutOfRange,
                        $"Point element {i} (line {pt.SourceLineNumber}) position index {posIdx} is out of bounds.",
                        ValidationSeverity.Error,
                        $"p[{i}]"));
                }
            }
        }

        return ValidationResult.Failure(issues);
    }
}

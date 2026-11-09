using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Materials;
using NEXUS3D.Formats.Obj.Textures;

namespace NEXUS3D.Formats.Obj.Validation;

/// <summary>
/// Validates Wavefront <see cref="MtlDocument"/> material properties, unique names, and texture references.
/// </summary>
public sealed class MtlValidator
{
    private readonly TextureValidationService _textureValidator;

    public MtlValidator(TextureValidationService? textureValidator = null)
    {
        _textureValidator = textureValidator ?? new TextureValidationService();
    }

    public ValidationResult Validate(
        MtlDocument document,
        string? baseDirectory = null,
        bool strictMode = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issues = new List<ValidationIssue>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < document.Materials.Count; i++)
        {
            MtlMaterial mat = document.Materials[i];
            if (string.IsNullOrWhiteSpace(mat.Name))
            {
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidMtlSyntax,
                    $"Material at index {i} (line {mat.LineNumber}) has an empty name.",
                    ValidationSeverity.Error,
                    $"Material[{i}]"));
            }
            else if (!seenNames.Add(mat.Name))
            {
                ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidMtlValue,
                    $"Duplicate material name '{mat.Name}' in MTL document (line {mat.LineNumber}).",
                    severity,
                    $"Material:{mat.Name}"));
            }

            ValidateColor(mat.AmbientColor, "Ka", mat.Name, issues);
            ValidateColor(mat.DiffuseColor, "Kd", mat.Name, issues);
            ValidateColor(mat.SpecularColor, "Ks", mat.Name, issues);
            ValidateColor(mat.EmissiveColor, "Ke", mat.Name, issues);
            ValidateColor(mat.TransmissionFilter, "Tf", mat.Name, issues);

            if (mat.SpecularExponent.HasValue && (!double.IsFinite(mat.SpecularExponent.Value) || mat.SpecularExponent.Value < 0.0))
            {
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidMtlValue,
                    $"Material '{mat.Name}' has invalid 'Ns' value: {mat.SpecularExponent.Value}.",
                    ValidationSeverity.Error,
                    $"Material:{mat.Name}"));
            }

            if (mat.OpticalDensity.HasValue && (!double.IsFinite(mat.OpticalDensity.Value) || mat.OpticalDensity.Value < 0.001 || mat.OpticalDensity.Value > 10.0))
            {
                ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidMtlValue,
                    $"Material '{mat.Name}' has 'Ni' value {mat.OpticalDensity.Value} outside standard [0.001, 10.0] range.",
                    severity,
                    $"Material:{mat.Name}"));
            }

            if (mat.Dissolve.HasValue && (!double.IsFinite(mat.Dissolve.Value) || mat.Dissolve.Value < 0.0 || mat.Dissolve.Value > 1.0))
            {
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidMtlValue,
                    $"Material '{mat.Name}' has 'd' value {mat.Dissolve.Value} outside [0.0, 1.0].",
                    ValidationSeverity.Error,
                    $"Material:{mat.Name}"));
            }

            if (mat.Transparency.HasValue && (!double.IsFinite(mat.Transparency.Value) || mat.Transparency.Value < 0.0 || mat.Transparency.Value > 1.0))
            {
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidMtlValue,
                    $"Material '{mat.Name}' has 'Tr' value {mat.Transparency.Value} outside [0.0, 1.0].",
                    ValidationSeverity.Error,
                    $"Material:{mat.Name}"));
            }

            if (mat.IlluminationModel.HasValue && (mat.IlluminationModel.Value < 0 || mat.IlluminationModel.Value > 10))
            {
                ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidMtlValue,
                    $"Material '{mat.Name}' has 'illum' value {mat.IlluminationModel.Value} outside [0, 10].",
                    severity,
                    $"Material:{mat.Name}"));
            }
        }

        var (_, textureIssues) = _textureValidator.ResolveAndValidate(document, baseDirectory, strictMode: false);
        issues.AddRange(textureIssues);

        return ValidationResult.Failure(issues);
    }

    private static void ValidateColor(
        MaterialColor? color,
        string keyword,
        string materialName,
        List<ValidationIssue> issues)
    {
        if (color.HasValue && !color.Value.IsValid)
        {
            issues.Add(new ValidationIssue(
                ObjErrorCodes.InvalidMtlValue,
                $"Material '{materialName}' has invalid '{keyword}' color: ({color.Value.R}, {color.Value.G}, {color.Value.B}).",
                ValidationSeverity.Error,
                $"Material:{materialName}"));
        }
    }
}

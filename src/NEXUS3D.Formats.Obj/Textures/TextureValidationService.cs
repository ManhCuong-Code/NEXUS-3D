using NEXUS3D.Domain.Textures;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Materials;

namespace NEXUS3D.Formats.Obj.Textures;

/// <summary>
/// Validates texture references inside Wavefront MTL documents and resolved <see cref="ObjTextureReference"/> descriptors.
/// </summary>
public sealed class TextureValidationService
{
    private readonly TexturePathResolver _pathResolver;

    public TextureValidationService(TexturePathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? new TexturePathResolver();
    }

    /// <summary>
    /// Resolves and validates all texture references declared across an <see cref="MtlDocument"/>.
    /// </summary>
    public (IReadOnlyList<ObjTextureReference> References, IReadOnlyList<ValidationIssue> Issues) ResolveAndValidate(
        MtlDocument document,
        string? baseDirectory,
        bool strictMode = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        var references = new List<ObjTextureReference>();
        var issues = new List<ValidationIssue>();

        string? effectiveBaseDir = baseDirectory;
        if (string.IsNullOrWhiteSpace(effectiveBaseDir) && !string.IsNullOrWhiteSpace(document.SourcePath))
        {
            effectiveBaseDir = Path.GetDirectoryName(document.SourcePath);
        }

        foreach (MtlMaterial material in document.Materials)
        {
            foreach (var (slotName, statement) in material.GetTextureMaps())
            {
                ObjTextureReference resolved = _pathResolver.ResolveReference(
                    material.Name,
                    slotName,
                    statement.Keyword,
                    statement.TexturePath,
                    effectiveBaseDir);

                references.Add(resolved);
                ValidateSingleReference(resolved, effectiveBaseDir is not null, strictMode, issues);
            }
        }

        return (references, issues);
    }

    /// <summary>
    /// Validates a single <see cref="ObjTextureReference"/> and appends any warnings or errors to <paramref name="issues"/>.
    /// </summary>
    public void ValidateSingleReference(
        ObjTextureReference reference,
        bool verifyDiskExistence,
        bool strictMode,
        List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(issues);

        if (string.IsNullOrWhiteSpace(reference.RawPath))
        {
            ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
            issues.Add(new ValidationIssue(
                ObjErrorCodes.MissingTextureFile,
                $"Material '{reference.MaterialName}' ({reference.MtlKeyword}) specifies an empty texture path.",
                severity,
                $"Material:{reference.MaterialName}"));
            return;
        }

        if (reference.Format == TextureFormat.Unknown)
        {
            issues.Add(new ValidationIssue(
                ObjErrorCodes.InvalidMtlValue,
                $"Material '{reference.MaterialName}' ({reference.MtlKeyword}) references texture '{reference.RawPath}' with an unrecognized image extension.",
                ValidationSeverity.Warning,
                $"Material:{reference.MaterialName}"));
        }

        if (verifyDiskExistence && !reference.ExistsOnDisk)
        {
            ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
            string message = $"Texture file '{reference.RawPath}' referenced by material '{reference.MaterialName}' ({reference.MtlKeyword}) was not found on disk.";
            issues.Add(new ValidationIssue(
                ObjErrorCodes.MissingTextureFile,
                message,
                severity,
                $"Material:{reference.MaterialName}"));

            if (strictMode)
            {
                throw new ObjException(ObjErrorCodes.MissingTextureFile, message);
            }
        }
    }
}


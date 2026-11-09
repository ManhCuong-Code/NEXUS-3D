using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Materials;
using NEXUS3D.Formats.Obj.Parsing;

namespace NEXUS3D.Formats.Obj.Validation;

/// <summary>
/// Comprehensive Wavefront OBJ and MTL validator implementing <see cref="IFormatValidator"/>.
/// Combines <see cref="ObjGeometryValidator"/>, <see cref="ObjReferenceValidator"/>, and <see cref="MtlValidator"/>.
/// </summary>
public sealed class ObjValidator : IFormatValidator
{
    private readonly ObjGeometryValidator _geometryValidator;
    private readonly ObjReferenceValidator _referenceValidator;
    private readonly MtlValidator _mtlValidator;
    private readonly IObjDocumentParser _objParser;

    public string FormatIdentifier => "wavefront:obj";

    public ObjValidator(
        ObjGeometryValidator? geometryValidator = null,
        ObjReferenceValidator? referenceValidator = null,
        MtlValidator? mtlValidator = null,
        IObjDocumentParser? objParser = null)
    {
        _geometryValidator = geometryValidator ?? new ObjGeometryValidator();
        _referenceValidator = referenceValidator ?? new ObjReferenceValidator();
        _mtlValidator = mtlValidator ?? new MtlValidator();
        _objParser = objParser ?? new ObjParser();
    }

    public ValidationResult ValidateDocument(
        ObjDocument document,
        MtlDocument? mtlDocument = null,
        string? baseDirectory = null,
        bool strictMode = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issues = new List<ValidationIssue>();

        if (!document.HasGeometry && document.MaterialLibraries.Count == 0)
        {
            issues.Add(new ValidationIssue(
                ObjErrorCodes.EmptyOrUnrecognizedFile,
                "OBJ document contains no geometry (v, f, l, p) or material references.",
                ValidationSeverity.Error,
                document.SourceFileName ?? "ObjDocument"));
        }

        for (int i = 0; i < document.Warnings.Count; i++)
        {
            var w = document.Warnings[i];
            ValidationSeverity severity = strictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
            issues.Add(new ValidationIssue(w.Code, w.Message, severity, w.SourceComponent));
        }

        ValidationResult geomResult = _geometryValidator.Validate(document, strictMode);
        issues.AddRange(geomResult.Issues);

        ValidationResult refResult = _referenceValidator.Validate(document, mtlDocument, strictMode);
        issues.AddRange(refResult.Issues);

        if (mtlDocument is not null)
        {
            ValidationResult mtlResult = _mtlValidator.Validate(mtlDocument, baseDirectory, strictMode);
            issues.AddRange(mtlResult.Issues);
        }

        return ValidationResult.Failure(issues);
    }

    public async Task<ValidationResult> ValidateStreamAsync(
        Stream source,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var parseOptions = new ObjParseOptions
            {
                StrictValidation = false,
                MaxFileSizeBytes = options?.MaxInputBytes ?? ObjParseOptions.Default.MaxFileSizeBytes,
                PreserveUnrecognizedStatements = options?.PreserveUnknownData ?? true,
                SourceFilePath = options?.SourceFileName
            };

            ObjDocument doc = await _objParser.ParseDocumentAsync(source, parseOptions, cancellationToken).ConfigureAwait(false);
            return ValidateDocument(
                doc,
                mtlDocument: null,
                baseDirectory: null,
                strictMode: options?.StrictValidation ?? false);
        }
        catch (ObjException ex)
        {
            return ValidationResult.Failure(
            [
                new ValidationIssue(
                    ex.ErrorCode,
                    ex.Message,
                    ValidationSeverity.Error,
                    ex.LineNumber.HasValue ? $"Line {ex.LineNumber.Value}" : options?.SourceFileName)
            ]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ValidationResult.Failure(
            [
                new ValidationIssue(
                    ObjErrorCodes.SyntaxError,
                    ex.Message,
                    ValidationSeverity.Error,
                    options?.SourceFileName)
            ]);
        }
    }
}

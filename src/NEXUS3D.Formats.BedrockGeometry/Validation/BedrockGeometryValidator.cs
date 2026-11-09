using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Validation;

/// <summary>
/// Validates a complete <see cref="BedrockGeometryDocument"/> including format version, descriptions,
/// bone hierarchies, and cubes.
/// </summary>
public sealed class BedrockGeometryValidator
{
    private readonly BedrockBoneValidator _boneValidator = new();

    public IReadOnlyList<BedrockGeometryDiagnostic> Validate(
        BedrockGeometryDocument document,
        BedrockGeometryReadOptions? options = null,
        string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= BedrockGeometryReadOptions.Default;

        var diagnostics = new List<BedrockGeometryDiagnostic>();

        if (string.IsNullOrWhiteSpace(document.FormatVersion))
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.MissingRequiredProperty,
                "Bedrock Geometry document is missing 'format_version'.",
                IsError: true,
                FilePath: filePath,
                JsonPath: "$.format_version"));
        }
        else if (BedrockGeometryDetector.ClassifyVersionString(document.FormatVersion) == BedrockGeometrySchemaFamily.Unknown)
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.UnsupportedSchemaOrDialect,
                $"Unrecognized Bedrock Geometry format_version '{document.FormatVersion}'. Treating as modern compatible schema.",
                IsError: options.StrictMode,
                FilePath: filePath,
                JsonPath: "$.format_version"));
        }

        if (document.Geometries.Count == 0)
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.MissingRequiredProperty,
                "Bedrock Geometry document contains no geometry definitions.",
                IsError: true,
                FilePath: filePath,
                JsonPath: "$"));
            return diagnostics;
        }

        var geometryIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < document.Geometries.Count; i++)
        {
            Models.BedrockGeometry geo = document.Geometries[i];
            string geoPath = document.SchemaFamily == BedrockGeometrySchemaFamily.Legacy180
                ? $"$['{geo.Description.Identifier}']"
                : $"$['minecraft:geometry'][{i}]";

            if (string.IsNullOrWhiteSpace(geo.Description.Identifier))
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.MissingRequiredProperty,
                    $"Geometry at index {i} is missing a valid 'identifier'.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{geoPath}.description.identifier"));
            }
            else if (!geometryIdentifiers.Add(geo.Description.Identifier))
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.DuplicateIdentifierOrCoordinate,
                    $"Duplicate geometry identifier '{geo.Description.Identifier}'.",
                    IsError: options.StrictMode,
                    FilePath: filePath,
                    JsonPath: $"{geoPath}.description.identifier"));
            }

            if (!geo.Description.HasValidTextureDimensions)
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    $"Geometry '{geo.Description.Identifier}' has non-positive texture dimensions ({geo.Description.TextureWidth}x{geo.Description.TextureHeight}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{geoPath}.description"));
            }

            if (!geo.Description.HasValidVisibleBounds)
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    $"Geometry '{geo.Description.Identifier}' has invalid visible bounds.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{geoPath}.description"));
            }

            _boneValidator.ValidateBones(
                geo.Bones,
                geo.Description,
                options,
                diagnostics,
                filePath,
                $"{geoPath}.bones");
        }

        return diagnostics;
    }
}

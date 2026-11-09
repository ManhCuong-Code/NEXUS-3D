using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Parsing;

/// <summary>
/// Parses Minecraft Bedrock Geometry (<c>.geo.json</c>) documents across Legacy (<c>1.8.0</c>, <c>1.10.0</c>),
/// Intermediate (<c>1.12.0</c>), and Modern (<c>1.16.0</c>, <c>1.19.0</c>, <c>1.21.0</c>) schema versions.
/// </summary>
public sealed class BedrockGeometryParser
{
    private static readonly HashSet<string> ModernRootKnownProperties = new(StringComparer.Ordinal)
    {
        "format_version", "minecraft:geometry"
    };

    private static readonly HashSet<string> ModernGeometryKnownProperties = new(StringComparer.Ordinal)
    {
        "description", "bones", "cape"
    };

    private static readonly HashSet<string> ModernDescriptionKnownProperties = new(StringComparer.Ordinal)
    {
        "identifier", "texture_width", "texture_height",
        "visible_bounds_width", "visible_bounds_height", "visible_bounds_offset"
    };

    private static readonly HashSet<string> LegacyGeometryKnownProperties = new(StringComparer.Ordinal)
    {
        "texturewidth", "textureheight", "texture_width", "texture_height",
        "visible_bounds_width", "visible_bounds_height", "visible_bounds_offset",
        "bones", "cape"
    };

    public BedrockGeometryParseResult ParseFile(string filePath, BedrockGeometryReadOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Parse(stream, options, filePath);
    }

    public BedrockGeometryParseResult Parse(string jsonText, BedrockGeometryReadOptions? options = null, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(jsonText);
        byte[] bytes = Encoding.UTF8.GetBytes(jsonText);
        return Parse(bytes.AsMemory(), options, filePath);
    }

    public BedrockGeometryParseResult Parse(Stream stream, BedrockGeometryReadOptions? options = null, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= BedrockGeometryReadOptions.Default;

        var sw = Stopwatch.StartNew();
        using JsonDocument jsonDoc = BedrockJsonExtensionHelpers.ParseDocument(stream, options, filePath);
        return ParseDocumentCore(jsonDoc.RootElement, options, filePath, sw);
    }

    public BedrockGeometryParseResult Parse(ReadOnlyMemory<byte> utf8Bytes, BedrockGeometryReadOptions? options = null, string? filePath = null)
    {
        options ??= BedrockGeometryReadOptions.Default;

        var sw = Stopwatch.StartNew();
        using JsonDocument jsonDoc = BedrockJsonExtensionHelpers.ParseDocument(utf8Bytes, options, filePath);
        return ParseDocumentCore(jsonDoc.RootElement, options, filePath, sw);
    }

    private static BedrockGeometryParseResult ParseDocumentCore(
        JsonElement root,
        BedrockGeometryReadOptions options,
        string? filePath,
        Stopwatch sw)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.EmptyOrInvalidRoot,
                "Bedrock Geometry JSON root must be a JSON object.",
                "$");
        }

        if (BedrockGeometryDetector.IsBedrockAnimationDocument(root))
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.MisplacedAnimationDocument,
                "The JSON document is a Minecraft Bedrock Animation file ('animations'), not a Bedrock Geometry (.geo.json) model.",
                "$.animations");
        }

        if (root.TryGetProperty("minecraft:geometry", out JsonElement rawGeoProp) &&
            rawGeoProp.ValueKind != JsonValueKind.Array)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.TypeMismatch,
                "Property 'minecraft:geometry' must be a JSON array.",
                "$['minecraft:geometry']");
        }

        if (!BedrockGeometryDetector.IsBedrockGeometryDocument(
                root,
                out BedrockGeometrySchemaFamily schemaFamily,
                out string formatVersion))
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.UnsupportedSchemaOrDialect,
                "Unrecognized Bedrock Geometry JSON structure. Expected 'minecraft:geometry' array or legacy 'geometry.<name>' root objects.",
                "$");
        }

        var diagnostics = new List<BedrockGeometryDiagnostic>();
        var document = new BedrockGeometryDocument
        {
            FormatVersion = formatVersion,
            SchemaFamily = schemaFamily
        };

        if (schemaFamily == BedrockGeometrySchemaFamily.Legacy180)
        {
            ParseLegacyDocument(root, document, options, diagnostics, filePath);
        }
        else
        {
            ParseModernOrIntermediateDocument(root, document, options, diagnostics, filePath);
        }

        if (options.StrictMode)
        {
            BedrockGeometryDiagnostic? firstError = diagnostics.FirstOrDefault(d => d.IsError);
            if (firstError is not null)
            {
                throw new JsonFormatException(
                    firstError.Code,
                    firstError.Message,
                    firstError.JsonPath);
            }
        }

        sw.Stop();
        return new BedrockGeometryParseResult
        {
            Document = document,
            Diagnostics = diagnostics,
            Elapsed = sw.Elapsed
        };
    }

    private static void ParseLegacyDocument(
        JsonElement root,
        BedrockGeometryDocument document,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath)
    {
        int geoCount = 0;
        foreach (JsonProperty prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, "format_version", StringComparison.Ordinal))
            {
                continue;
            }

            if (!prop.Name.StartsWith("geometry.", StringComparison.OrdinalIgnoreCase))
            {
                if (options.PreserveExtensionData)
                {
                    document.RootExtensionData.Set(prop.Name, prop.Value);
                }

                continue;
            }

            if (geoCount >= options.MaxGeometryCount)
            {
                throw new JsonFormatException(
                    JsonFormatErrorCodes.LimitExceeded,
                    $"Geometry count exceeds maximum allowed ({options.MaxGeometryCount}).",
                    $"$.{prop.Name}");
            }

            string geoPath = $"$['{prop.Name}']";
            if (prop.Value.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.TypeMismatch,
                    $"Legacy geometry entry '{prop.Name}' must be a JSON object.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: geoPath));
                continue;
            }

            string identifier = prop.Name;
            string? parentIdentifier = null;
            int colonIdx = prop.Name.IndexOf(':', StringComparison.Ordinal);
            if (colonIdx > 0 && colonIdx < prop.Name.Length - 1)
            {
                identifier = prop.Name[..colonIdx].Trim();
                parentIdentifier = prop.Name[(colonIdx + 1)..].Trim();
            }

            var geometry = new Models.BedrockGeometry
            {
                ParentGeometryIdentifier = parentIdentifier,
                Description = new BedrockGeometryDescription
                {
                    Identifier = identifier
                }
            };

            JsonElement geoObj = prop.Value;

            if ((geoObj.TryGetProperty("texturewidth", out JsonElement twProp) ||
                 geoObj.TryGetProperty("texture_width", out twProp)) &&
                twProp.ValueKind == JsonValueKind.Number &&
                twProp.TryGetInt32(out int tw))
            {
                geometry.Description.TextureWidth = tw;
            }

            if ((geoObj.TryGetProperty("textureheight", out JsonElement thProp) ||
                 geoObj.TryGetProperty("texture_height", out thProp)) &&
                thProp.ValueKind == JsonValueKind.Number &&
                thProp.TryGetInt32(out int th))
            {
                geometry.Description.TextureHeight = th;
            }

            ParseVisibleBounds(geoObj, geometry.Description, diagnostics, filePath, geoPath);

            if (geoObj.TryGetProperty("cape", out JsonElement capeProp) && capeProp.ValueKind == JsonValueKind.String)
            {
                geometry.Cape = capeProp.GetString();
            }

            if (geoObj.TryGetProperty("bones", out JsonElement bonesProp))
            {
                if (bonesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement boneElem in bonesProp.EnumerateArray())
                    {
                        geometry.RawBoneElements.Add(boneElem.Clone());
                    }
                }
                else
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.TypeMismatch,
                        $"Property 'bones' in '{identifier}' must be a JSON array.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{geoPath}.bones"));
                }
            }

            if (options.PreserveExtensionData)
            {
                geometry.ExtensionData.CaptureUnknownProperties(geoObj, LegacyGeometryKnownProperties);
            }

            document.Geometries.Add(geometry);
            geoCount++;
        }
    }

    private static void ParseModernOrIntermediateDocument(
        JsonElement root,
        BedrockGeometryDocument document,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath)
    {
        if (options.PreserveExtensionData)
        {
            document.RootExtensionData.CaptureUnknownProperties(root, ModernRootKnownProperties);
        }

        if (!root.TryGetProperty("minecraft:geometry", out JsonElement geoArray) ||
            geoArray.ValueKind != JsonValueKind.Array)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.MissingRequiredProperty,
                "Bedrock Geometry document is missing required 'minecraft:geometry' array.",
                "$['minecraft:geometry']");
        }

        int index = 0;
        foreach (JsonElement geoObj in geoArray.EnumerateArray())
        {
            if (index >= options.MaxGeometryCount)
            {
                throw new JsonFormatException(
                    JsonFormatErrorCodes.LimitExceeded,
                    $"Geometry count exceeds maximum allowed ({options.MaxGeometryCount}).",
                    $"$['minecraft:geometry'][{index}]");
            }

            string geoPath = $"$['minecraft:geometry'][{index}]";
            if (geoObj.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.TypeMismatch,
                    $"Geometry entry at index {index} must be a JSON object.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: geoPath));
                index++;
                continue;
            }

            var geometry = new Models.BedrockGeometry();
            if (geoObj.TryGetProperty("description", out JsonElement descObj) &&
                descObj.ValueKind == JsonValueKind.Object)
            {
                ParseDescriptionObject(descObj, geometry.Description, options, diagnostics, filePath, $"{geoPath}.description");
            }
            else
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.MissingRequiredProperty,
                    $"Geometry entry at index {index} is missing required 'description' object.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{geoPath}.description"));
            }

            if (geoObj.TryGetProperty("cape", out JsonElement capeProp) && capeProp.ValueKind == JsonValueKind.String)
            {
                geometry.Cape = capeProp.GetString();
            }

            if (geoObj.TryGetProperty("bones", out JsonElement bonesProp))
            {
                if (bonesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement boneElem in bonesProp.EnumerateArray())
                    {
                        geometry.RawBoneElements.Add(boneElem.Clone());
                    }
                }
                else
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.TypeMismatch,
                        $"Property 'bones' at '{geoPath}.bones' must be a JSON array.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{geoPath}.bones"));
                }
            }

            if (options.PreserveExtensionData)
            {
                geometry.ExtensionData.CaptureUnknownProperties(geoObj, ModernGeometryKnownProperties);
            }

            document.Geometries.Add(geometry);
            index++;
        }
    }

    private static void ParseDescriptionObject(
        JsonElement descObj,
        BedrockGeometryDescription description,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        if (descObj.TryGetProperty("identifier", out JsonElement idProp) &&
            idProp.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(idProp.GetString()))
        {
            description.Identifier = idProp.GetString()!.Trim();
        }
        else
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.MissingRequiredProperty,
                "Geometry description is missing a non-empty 'identifier'.",
                IsError: true,
                FilePath: filePath,
                JsonPath: $"{jsonPath}.identifier"));
        }

        if (descObj.TryGetProperty("texture_width", out JsonElement twProp))
        {
            if (twProp.ValueKind == JsonValueKind.Number && twProp.TryGetInt32(out int tw) && tw > 0)
            {
                description.TextureWidth = tw;
            }
            else
            {
                description.TextureWidth = -1;
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Geometry 'texture_width' must be a positive integer.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.texture_width"));
            }
        }

        if (descObj.TryGetProperty("texture_height", out JsonElement thProp))
        {
            if (thProp.ValueKind == JsonValueKind.Number && thProp.TryGetInt32(out int th) && th > 0)
            {
                description.TextureHeight = th;
            }
            else
            {
                description.TextureHeight = -1;
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Geometry 'texture_height' must be a positive integer.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.texture_height"));
            }
        }

        ParseVisibleBounds(descObj, description, diagnostics, filePath, jsonPath);

        if (options.PreserveExtensionData)
        {
            description.ExtensionData.CaptureUnknownProperties(descObj, ModernDescriptionKnownProperties);
        }
    }

    private static void ParseVisibleBounds(
        JsonElement obj,
        BedrockGeometryDescription description,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        if (obj.TryGetProperty("visible_bounds_width", out JsonElement vbwProp))
        {
            if (vbwProp.ValueKind == JsonValueKind.Number && vbwProp.TryGetDouble(out double vbw) && double.IsFinite(vbw))
            {
                description.VisibleBoundsWidth = vbw;
            }
            else
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Property 'visible_bounds_width' must be a finite number.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.visible_bounds_width"));
            }
        }

        if (obj.TryGetProperty("visible_bounds_height", out JsonElement vbhProp))
        {
            if (vbhProp.ValueKind == JsonValueKind.Number && vbhProp.TryGetDouble(out double vbh) && double.IsFinite(vbh))
            {
                description.VisibleBoundsHeight = vbh;
            }
            else
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Property 'visible_bounds_height' must be a finite number.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.visible_bounds_height"));
            }
        }

        if (obj.TryGetProperty("visible_bounds_offset", out JsonElement vboProp))
        {
            if (TryParseVector3D(vboProp, out Vector3D vbo))
            {
                description.VisibleBoundsOffset = vbo;
            }
            else
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Property 'visible_bounds_offset' must be a 3-number array [x, y, z].",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.visible_bounds_offset"));
            }
        }
    }

    internal static bool TryParseVector3D(JsonElement element, out Vector3D vector)
    {
        vector = default;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 3)
        {
            return false;
        }

        using JsonElement.ArrayEnumerator e = element.EnumerateArray();
        e.MoveNext();
        if (!TryReadFiniteDouble(e.Current, out double x)) return false;
        e.MoveNext();
        if (!TryReadFiniteDouble(e.Current, out double y)) return false;
        e.MoveNext();
        if (!TryReadFiniteDouble(e.Current, out double z)) return false;

        vector = new Vector3D(x, y, z);
        return true;
    }

    internal static bool TryParseVector2D(JsonElement element, out Vector2D vector)
    {
        vector = default;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 2)
        {
            return false;
        }

        using JsonElement.ArrayEnumerator e = element.EnumerateArray();
        e.MoveNext();
        if (!TryReadFiniteDouble(e.Current, out double u)) return false;
        e.MoveNext();
        if (!TryReadFiniteDouble(e.Current, out double v)) return false;

        vector = new Vector2D(u, v);
        return true;
    }

    internal static bool TryReadFiniteDouble(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value) && double.IsFinite(value))
        {
            return true;
        }

        value = 0.0;
        return false;
    }
}

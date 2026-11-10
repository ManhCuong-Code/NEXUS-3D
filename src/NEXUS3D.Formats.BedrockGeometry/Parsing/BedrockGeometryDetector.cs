using System.Text.Json;
using NEXUS3D.Formats.BedrockGeometry.Models;

namespace NEXUS3D.Formats.BedrockGeometry.Parsing;

/// <summary>
/// Inspects JSON root elements to identify Minecraft Bedrock Geometry schema versions (<c>1.8.0</c>, <c>1.10.0</c>,
/// <c>1.12.0</c>, <c>1.16.0</c>, <c>1.19.0</c>, <c>1.21.0</c>) and detect misplaced Bedrock Animation JSON files.
/// </summary>
public static class BedrockGeometryDetector
{
    public static bool IsBedrockAnimationDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return root.TryGetProperty("animations", out JsonElement animProp) &&
               animProp.ValueKind == JsonValueKind.Object &&
               !root.TryGetProperty("minecraft:geometry", out _) &&
               !HasLegacyGeometryProperties(root);
    }

    public static bool IsBedrockGeometryDocument(JsonElement root, out BedrockGeometrySchemaFamily schemaFamily, out string formatVersion)
    {
        schemaFamily = BedrockGeometrySchemaFamily.Unknown;
        formatVersion = string.Empty;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("format_version", out JsonElement verProp) &&
            verProp.ValueKind == JsonValueKind.String)
        {
            formatVersion = verProp.GetString()?.Trim() ?? string.Empty;
        }

        if (root.TryGetProperty("minecraft:geometry", out JsonElement geoProp) &&
            geoProp.ValueKind == JsonValueKind.Array)
        {
            if (string.IsNullOrWhiteSpace(formatVersion))
            {
                formatVersion = "1.12.0";
            }

            schemaFamily = ClassifyVersionString(formatVersion);
            if (schemaFamily == BedrockGeometrySchemaFamily.Legacy180 || schemaFamily == BedrockGeometrySchemaFamily.Unknown)
            {
                schemaFamily = BedrockGeometrySchemaFamily.Intermediate1120;
            }

            return true;
        }

        if (HasLegacyGeometryProperties(root))
        {
            if (string.IsNullOrWhiteSpace(formatVersion))
            {
                formatVersion = "1.8.0";
            }

            schemaFamily = BedrockGeometrySchemaFamily.Legacy180;
            return true;
        }

        return false;
    }

    public static BedrockGeometrySchemaFamily ClassifyVersionString(string? formatVersion)
    {
        if (string.IsNullOrWhiteSpace(formatVersion))
        {
            return BedrockGeometrySchemaFamily.Unknown;
        }

        string trimmed = formatVersion.Trim();
        if (trimmed.StartsWith("1.8", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("1.10", StringComparison.OrdinalIgnoreCase))
        {
            return BedrockGeometrySchemaFamily.Legacy180;
        }

        if (trimmed.StartsWith("1.12", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("1.14", StringComparison.OrdinalIgnoreCase))
        {
            return BedrockGeometrySchemaFamily.Intermediate1120;
        }

        if (trimmed.StartsWith("1.16", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("1.17", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("1.18", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("1.19", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("1.20", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("1.21", StringComparison.OrdinalIgnoreCase))
        {
            return BedrockGeometrySchemaFamily.Modern1160Plus;
        }

        return BedrockGeometrySchemaFamily.Unknown;
    }

    private static bool HasLegacyGeometryProperties(JsonElement root)
    {
        foreach (JsonProperty prop in root.EnumerateObject())
        {
            if (prop.Name.StartsWith("geometry.", StringComparison.OrdinalIgnoreCase) &&
                prop.Value.ValueKind == JsonValueKind.Object)
            {
                return true;
            }
        }

        return false;
    }
}

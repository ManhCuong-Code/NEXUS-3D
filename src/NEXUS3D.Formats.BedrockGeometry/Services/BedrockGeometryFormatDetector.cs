using System.Text.Json;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Services;

public interface IBedrockGeometryFormatDetector : IFormatDetector
{
    bool TryDetectSchema(Stream source, out BedrockGeometrySchemaFamily schemaFamily, out string formatVersion);
}

/// <summary>
/// Inspects JSON streams to detect Minecraft Bedrock Geometry (<c>.geo.json</c>) documents across
/// <c>1.8.0</c>, <c>1.10.0</c>, <c>1.12.0</c>, <c>1.16.0</c>, <c>1.19.0</c>, and <c>1.21.0</c>.
/// </summary>
public sealed class BedrockGeometryFormatDetector : IBedrockGeometryFormatDetector
{
    public async Task<FormatDetectionResult> DetectAsync(
        Stream source,
        string? fileExtensionHint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        long originalPos = source.CanSeek ? source.Position : 0;
        try
        {
            using JsonDocument doc = await JsonParsingUtilities.ParseBoundedDocumentAsync(
                source,
                JsonReadOptions.Default,
                cancellationToken).ConfigureAwait(false);

            if (!BedrockGeometryDetector.IsBedrockGeometryDocument(
                    doc.RootElement,
                    out BedrockGeometrySchemaFamily schemaFamily,
                    out string formatVersion))
            {
                return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "Not a recognized Bedrock Geometry JSON document.");
            }

            int versionNumber = schemaFamily switch
            {
                BedrockGeometrySchemaFamily.Legacy180 => 108,
                BedrockGeometrySchemaFamily.Intermediate1120 => 112,
                BedrockGeometrySchemaFamily.Modern1160Plus => 116,
                _ => 112
            };

            return new FormatDetectionResult(
                IsRecognized: true,
                FormatType: ModelFormatType.BedrockGeometry,
                FormatId: "minecraft:bedrock_geometry",
                Version: versionNumber,
                Details: $"Minecraft Bedrock Geometry ({formatVersion}, {schemaFamily})");
        }
        catch
        {
            return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "Invalid or non-Bedrock Geometry JSON stream.");
        }
        finally
        {
            if (source.CanSeek)
            {
                source.Position = originalPos;
            }
        }
    }

    public bool TryDetectSchema(Stream source, out BedrockGeometrySchemaFamily schemaFamily, out string formatVersion)
    {
        ArgumentNullException.ThrowIfNull(source);
        schemaFamily = BedrockGeometrySchemaFamily.Unknown;
        formatVersion = string.Empty;

        long originalPos = source.CanSeek ? source.Position : 0;
        try
        {
            using JsonDocument doc = BedrockJsonExtensionHelpers.ParseDocument(source, JsonReadOptions.Default);
            return BedrockGeometryDetector.IsBedrockGeometryDocument(doc.RootElement, out schemaFamily, out formatVersion);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (source.CanSeek)
            {
                source.Position = originalPos;
            }
        }
    }
}

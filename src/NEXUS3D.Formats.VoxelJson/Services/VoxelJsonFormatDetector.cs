using System.Text.Json;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;

namespace NEXUS3D.Formats.VoxelJson.Services;

public interface IVoxelJsonFormatDetector : IFormatDetector
{
    VoxelJsonDialect DetectDialect(Stream source);
}

/// <summary>
/// Inspects JSON streams to detect Voxel JSON documents without misclassifying Bedrock Geometry or Animation files.
/// </summary>
public sealed class VoxelJsonFormatDetector : IVoxelJsonFormatDetector
{
    public async Task<FormatDetectionResult> DetectAsync(
        Stream source,
        string? fileExtensionHint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!string.IsNullOrWhiteSpace(fileExtensionHint) &&
            fileExtensionHint.Trim().EndsWith(".geo.json", StringComparison.OrdinalIgnoreCase))
        {
            return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "Bedrock .geo.json extension.");
        }

        long originalPos = source.CanSeek ? source.Position : 0;
        try
        {
            using JsonDocument doc = await JsonParsingUtilities.ParseBoundedDocumentAsync(
                source,
                JsonReadOptions.Default,
                cancellationToken).ConfigureAwait(false);

            VoxelJsonDialect dialect = VoxelJsonDialectDetector.Detect(doc.RootElement);
            if (dialect == VoxelJsonDialect.Unknown)
            {
                return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "Not a recognized Voxel JSON dialect.");
            }

            string formatId = dialect switch
            {
                VoxelJsonDialect.NexusVoxelGridV1 => "nexus:voxel_grid_v1",
                VoxelJsonDialect.SparseVoxelArray => "voxel:sparse_array",
                VoxelJsonDialect.MinecraftBlockModelCuboids => "minecraft:block_model_cuboids",
                _ => "voxel:json"
            };

            return new FormatDetectionResult(
                IsRecognized: true,
                FormatType: ModelFormatType.VoxelJson,
                FormatId: formatId,
                Version: 1,
                Details: $"Voxel JSON ({dialect})");
        }
        catch
        {
            return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "Invalid or non-Voxel JSON stream.");
        }
        finally
        {
            if (source.CanSeek)
            {
                source.Position = originalPos;
            }
        }
    }

    public VoxelJsonDialect DetectDialect(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        long originalPos = source.CanSeek ? source.Position : 0;
        try
        {
            using JsonDocument doc = VoxelJsonExtensionHelpers.ParseDocument(source, JsonReadOptions.Default);
            return VoxelJsonDialectDetector.Detect(doc.RootElement);
        }
        catch
        {
            return VoxelJsonDialect.Unknown;
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

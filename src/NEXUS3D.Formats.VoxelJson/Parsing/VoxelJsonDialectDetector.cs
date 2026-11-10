using System.Text.Json;
using NEXUS3D.Formats.VoxelJson.Models;

namespace NEXUS3D.Formats.VoxelJson.Parsing;

/// <summary>
/// Inspects a JSON root element to determine its <see cref="VoxelJsonDialect"/> and distinguish it from
/// Minecraft Bedrock Geometry or Animation JSON documents.
/// </summary>
public static class VoxelJsonDialectDetector
{
    public static VoxelJsonDialect Detect(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return VoxelJsonDialect.Unknown;
        }

        if (IsBedrockAnimationDocument(root) || IsBedrockGeometryDocument(root))
        {
            return VoxelJsonDialect.Unknown;
        }

        if (root.TryGetProperty("format", out JsonElement formatProp) &&
            formatProp.ValueKind == JsonValueKind.String)
        {
            string? format = formatProp.GetString();
            if (string.Equals(format, "nexus:voxel_grid_v1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(format, "nexus-voxel-v1", StringComparison.OrdinalIgnoreCase))
            {
                return VoxelJsonDialect.NexusVoxelGridV1;
            }

            if (string.Equals(format, "voxel:sparse_array", StringComparison.OrdinalIgnoreCase))
            {
                return VoxelJsonDialect.SparseVoxelArray;
            }

            if (string.Equals(format, "minecraft:block_model_cuboids", StringComparison.OrdinalIgnoreCase))
            {
                return VoxelJsonDialect.MinecraftBlockModelCuboids;
            }
        }

        bool hasGridObject = root.TryGetProperty("grid", out JsonElement gridProp) && gridProp.ValueKind == JsonValueKind.Object;
        bool hasVoxelsProp = root.TryGetProperty("voxels", out JsonElement voxelsProp);
        bool hasVoxelsArray = hasVoxelsProp && voxelsProp.ValueKind == JsonValueKind.Array;

        if (hasGridObject && (hasVoxelsArray || !hasVoxelsProp))
        {
            return VoxelJsonDialect.NexusVoxelGridV1;
        }

        if (root.TryGetProperty("elements", out JsonElement elementsProp) &&
            elementsProp.ValueKind == JsonValueKind.Array)
        {
            return VoxelJsonDialect.MinecraftBlockModelCuboids;
        }

        if (hasVoxelsArray)
        {
            return VoxelJsonDialect.SparseVoxelArray;
        }

        return VoxelJsonDialect.Unknown;
    }

    public static bool IsBedrockAnimationDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return root.TryGetProperty("animations", out JsonElement animProp) &&
               animProp.ValueKind == JsonValueKind.Object &&
               !root.TryGetProperty("minecraft:geometry", out _) &&
               !root.TryGetProperty("voxels", out _) &&
               !root.TryGetProperty("elements", out _);
    }

    public static bool IsBedrockGeometryDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("minecraft:geometry", out _))
        {
            return true;
        }

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

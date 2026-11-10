using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Models;

/// <summary>
/// Categorizes the Minecraft Bedrock Geometry schema family.
/// </summary>
public enum BedrockGeometrySchemaFamily
{
    Unknown = 0,

    /// <summary>Legacy root object schema (<c>1.8.0</c>, <c>1.10.0</c>) with <c>"geometry.&lt;name&gt;"</c> properties at root.</summary>
    Legacy180 = 1,

    /// <summary>Intermediate <c>1.12.0</c> schema with <c>"minecraft:geometry"</c> array and <c>"description"</c> block.</summary>
    Intermediate1120 = 2,

    /// <summary>Modern <c>1.16.0</c>, <c>1.19.0</c>, <c>1.21.0</c> schema with per-cube pivot/rotation, per-face UV, locators, and binding.</summary>
    Modern1160Plus = 3
}

/// <summary>
/// Options controlling Minecraft Bedrock Geometry JSON parsing and validation.
/// </summary>
public sealed record BedrockGeometryReadOptions : JsonReadOptions
{
    public static new BedrockGeometryReadOptions Default { get; } = new();

    public int MaxGeometryCount { get; init; } = 256;
    public int MaxBonesPerGeometry { get; init; } = 4096;
    public int MaxCubesPerBone { get; init; } = 16384;
}

/// <summary>
/// Root in-memory representation of a Minecraft Bedrock Geometry (<c>.geo.json</c>) document.
/// A single file can contain one or multiple <see cref="BedrockGeometry"/> definitions.
/// </summary>
public sealed class BedrockGeometryDocument
{
    public string FormatVersion { get; set; } = "1.12.0";
    public BedrockGeometrySchemaFamily SchemaFamily { get; set; } = BedrockGeometrySchemaFamily.Intermediate1120;
    public List<BedrockGeometry> Geometries { get; set; } = [];
    public JsonExtensionData RootExtensionData { get; set; } = new();

    public BedrockGeometryDocument DeepClone() => new()
    {
        FormatVersion = FormatVersion,
        SchemaFamily = SchemaFamily,
        Geometries = Geometries.Select(g => g.DeepClone()).ToList(),
        RootExtensionData = RootExtensionData.DeepClone()
    };
}

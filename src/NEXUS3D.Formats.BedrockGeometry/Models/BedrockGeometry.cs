using System.Text.Json;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Models;

/// <summary>
/// Represents a single geometry definition within a <see cref="BedrockGeometryDocument"/>.
/// </summary>
public sealed class BedrockGeometry
{
    public BedrockGeometryDescription Description { get; set; } = new();

    /// <summary>Optional parent geometry identifier (used by legacy <c>geometry.child:geometry.parent</c> inheritance).</summary>
    public string? ParentGeometryIdentifier { get; set; }

    /// <summary>Optional cape identifier in Bedrock geometry.</summary>
    public string? Cape { get; set; }

    /// <summary>Raw JSON array of bones preserved for hierarchy parsing and inspection.</summary>
    public List<JsonElement> RawBoneElements { get; set; } = [];

    public JsonExtensionData ExtensionData { get; set; } = new();

    public BedrockGeometry DeepClone() => new()
    {
        Description = Description.DeepClone(),
        ParentGeometryIdentifier = ParentGeometryIdentifier,
        Cape = Cape,
        RawBoneElements = RawBoneElements.Select(e => e.Clone()).ToList(),
        ExtensionData = ExtensionData.DeepClone()
    };
}

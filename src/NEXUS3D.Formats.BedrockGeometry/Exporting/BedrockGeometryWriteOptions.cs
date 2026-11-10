using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Exporting;

/// <summary>
/// Options controlling Minecraft Bedrock Geometry (<c>.geo.json</c>) serialization and target schema version.
/// </summary>
public sealed record BedrockGeometryWriteOptions : JsonWriteOptions
{
    public static new BedrockGeometryWriteOptions Default { get; } = new();

    /// <summary>
    /// Optional target <c>format_version</c> override (e.g., <c>"1.8.0"</c>, <c>"1.12.0"</c>, <c>"1.16.0"</c>, <c>"1.21.0"</c>).
    /// When <see langword="null"/>, exports using the document's existing <c>FormatVersion</c>.
    /// </summary>
    public string? TargetFormatVersion { get; init; }

    /// <summary>
    /// Whether to topologically sort bones from root to leaves on export when <see cref="JsonWriteOptions.PreserveOrder"/> is false.
    /// </summary>
    public bool SortBonesTopologically { get; init; }
}

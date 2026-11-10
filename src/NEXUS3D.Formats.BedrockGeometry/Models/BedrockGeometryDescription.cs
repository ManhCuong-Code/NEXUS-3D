using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Models;

/// <summary>
/// Metadata and texture/bounds settings for a single <see cref="BedrockGeometry"/> definition.
/// </summary>
public sealed class BedrockGeometryDescription
{
    /// <summary>Geometry identifier, e.g. <c>"geometry.humanoid.custom"</c> or <c>"geometry.dragon"</c>.</summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>Texture atlas width in pixels (default 64).</summary>
    public int TextureWidth { get; set; } = 64;

    /// <summary>Texture atlas height in pixels (default 64).</summary>
    public int TextureHeight { get; set; } = 64;

    /// <summary>Visible bounding box width in blocks.</summary>
    public double? VisibleBoundsWidth { get; set; }

    /// <summary>Visible bounding box height in blocks.</summary>
    public double? VisibleBoundsHeight { get; set; }

    /// <summary>Visible bounding box center offset in blocks.</summary>
    public Vector3D? VisibleBoundsOffset { get; set; }

    public JsonExtensionData ExtensionData { get; set; } = new();

    public bool HasValidTextureDimensions => TextureWidth > 0 && TextureHeight > 0;

    public bool HasValidVisibleBounds =>
        (!VisibleBoundsWidth.HasValue || (double.IsFinite(VisibleBoundsWidth.Value) && VisibleBoundsWidth.Value >= 0.0)) &&
        (!VisibleBoundsHeight.HasValue || (double.IsFinite(VisibleBoundsHeight.Value) && VisibleBoundsHeight.Value >= 0.0)) &&
        (!VisibleBoundsOffset.HasValue || VisibleBoundsOffset.Value.IsFinite());

    public BedrockGeometryDescription DeepClone() => new()
    {
        Identifier = Identifier,
        TextureWidth = TextureWidth,
        TextureHeight = TextureHeight,
        VisibleBoundsWidth = VisibleBoundsWidth,
        VisibleBoundsHeight = VisibleBoundsHeight,
        VisibleBoundsOffset = VisibleBoundsOffset,
        ExtensionData = ExtensionData.DeepClone()
    };
}

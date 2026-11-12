using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Models;

/// <summary>
/// Distinguishes between Bedrock Box UV (<c>"uv": [u, v]</c>) and Per-Face UV (<c>"uv": { "north": { ... }, ... }</c>).
/// </summary>
public enum BedrockUvMode
{
    None = 0,
    BoxUv = 1,
    PerFaceUv = 2
}

/// <summary>
/// Per-face UV mapping entry inside a Bedrock cube's per-face <c>"uv"</c> object.
/// </summary>
public sealed class BedrockFaceUv
{
    public Vector2D Uv { get; set; }
    public Vector2D? UvSize { get; set; }
    public string? MaterialInstance { get; set; }
    public int? UvRotation { get; set; }
    public JsonExtensionData ExtensionData { get; set; } = new();

    public bool HasValidValues =>
        Uv.IsFinite() &&
        (!UvSize.HasValue || UvSize.Value.IsFinite());

    public BedrockFaceUv DeepClone() => new()
    {
        Uv = Uv,
        UvSize = UvSize,
        MaterialInstance = MaterialInstance,
        UvRotation = UvRotation,
        ExtensionData = ExtensionData.DeepClone()
    };
}

/// <summary>
/// Represents the UV mapping of a <see cref="BedrockCube"/>, supporting both Box UV and Per-Face UV.
/// </summary>
public sealed class BedrockCubeUv
{
    public static readonly IReadOnlyList<string> StandardFaceNames =
        ["north", "south", "east", "west", "up", "down"];

    public BedrockUvMode Mode { get; set; } = BedrockUvMode.BoxUv;

    /// <summary>Top-left texture offset <c>[u, v]</c> when <see cref="Mode"/> is <see cref="BedrockUvMode.BoxUv"/>.</summary>
    public Vector2D BoxUvOffset { get; set; } = Vector2D.Zero;

    /// <summary>Per-face UV mappings keyed by face direction (<c>north</c>, <c>south</c>, <c>east</c>, <c>west</c>, <c>up</c>, <c>down</c>).</summary>
    public Dictionary<string, BedrockFaceUv> Faces { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public JsonExtensionData ExtensionData { get; set; } = new();

    public static BedrockCubeUv CreateBoxUv(double u, double v) => new()
    {
        Mode = BedrockUvMode.BoxUv,
        BoxUvOffset = new Vector2D(u, v)
    };

    public static BedrockCubeUv CreatePerFaceUv() => new()
    {
        Mode = BedrockUvMode.PerFaceUv
    };

    public BedrockCubeUv DeepClone()
    {
        var clone = new BedrockCubeUv
        {
            Mode = Mode,
            BoxUvOffset = BoxUvOffset,
            ExtensionData = ExtensionData.DeepClone()
        };

        foreach ((string key, BedrockFaceUv face) in Faces)
        {
            clone.Faces[key] = face.DeepClone();
        }

        return clone;
    }
}

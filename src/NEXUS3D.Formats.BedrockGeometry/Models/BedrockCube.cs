using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Models;

/// <summary>
/// Represents a 3D cuboid primitive attached to a <see cref="BedrockBone"/>.
/// </summary>
public sealed class BedrockCube
{
    /// <summary>Lower-left-front corner of the unrotated cube in Bedrock pixel units <c>[x, y, z]</c>.</summary>
    public Vector3D Origin { get; set; }

    /// <summary>Cube dimensions in Bedrock pixel units <c>[width, height, depth]</c>.</summary>
    public Vector3D Size { get; set; }

    /// <summary>Optional per-cube rotation pivot <c>[x, y, z]</c> (supported in Bedrock 1.12.0+).</summary>
    public Vector3D? Pivot { get; set; }

    /// <summary>Optional per-cube Euler rotation in degrees <c>[x, y, z]</c> (supported in Bedrock 1.12.0+).</summary>
    public Vector3D? Rotation { get; set; }

    /// <summary>Optional uniform inflation offset in pixel units (can be negative, zero, or positive).</summary>
    public double? Inflate { get; set; }

    /// <summary>Optional UV horizontal mirror override for this cube.</summary>
    public bool? Mirror { get; set; }

    public JsonExtensionData ExtensionData { get; set; } = new();

    public bool HasValidOrigin => Origin.IsFinite();

    public bool HasValidSize =>
        Size.IsFinite() &&
        Size.X >= 0.0 &&
        Size.Y >= 0.0 &&
        Size.Z >= 0.0;

    public BedrockCube DeepClone() => new()
    {
        Origin = Origin,
        Size = Size,
        Pivot = Pivot,
        Rotation = Rotation,
        Inflate = Inflate,
        Mirror = Mirror,
        ExtensionData = ExtensionData.DeepClone()
    };
}

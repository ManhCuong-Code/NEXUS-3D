using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Models;

/// <summary>
/// Named attachment/effect locator on a <see cref="BedrockBone"/>.
/// Supports both compact <c>[x, y, z]</c> array syntax and extended <c>{ "offset": [...], "rotation": [...], "ignore_inherited_scale": true }</c> object syntax.
/// </summary>
public sealed class BedrockLocator
{
    public string Name { get; set; } = string.Empty;
    public Vector3D Offset { get; set; } = Vector3D.Zero;
    public Vector3D? Rotation { get; set; }
    public bool? IgnoreInheritedScale { get; set; }
    public bool IsObjectForm { get; set; }
    public JsonExtensionData ExtensionData { get; set; } = new();

    public bool HasValidCoordinates =>
        Offset.IsFinite() &&
        (!Rotation.HasValue || Rotation.Value.IsFinite());

    public BedrockLocator DeepClone() => new()
    {
        Name = Name,
        Offset = Offset,
        Rotation = Rotation,
        IgnoreInheritedScale = IgnoreInheritedScale,
        IsObjectForm = IsObjectForm,
        ExtensionData = ExtensionData.DeepClone()
    };
}

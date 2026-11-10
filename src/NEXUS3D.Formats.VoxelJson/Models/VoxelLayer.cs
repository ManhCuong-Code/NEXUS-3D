using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.VoxelJson.Models;

/// <summary>
/// Represents a logical layer or group in a <see cref="VoxelDocument"/>.
/// </summary>
public sealed class VoxelLayerDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Default";
    public string? ParentLayerId { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public double Opacity { get; set; } = 1.0;
    public Vector3D? Pivot { get; set; }
    public Vector3D? Rotation { get; set; }
    public List<int> ElementIndices { get; set; } = [];
    public JsonExtensionData ExtensionData { get; set; } = new();

    public VoxelLayerDefinition DeepClone() => new()
    {
        Id = Id,
        Name = Name,
        ParentLayerId = ParentLayerId,
        IsVisible = IsVisible,
        IsLocked = IsLocked,
        Opacity = Opacity,
        Pivot = Pivot,
        Rotation = Rotation,
        ElementIndices = [.. ElementIndices],
        ExtensionData = ExtensionData.DeepClone()
    };
}

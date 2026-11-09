using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Models;

/// <summary>
/// Represents a bone node in a Minecraft Bedrock Geometry skeleton hierarchy.
/// </summary>
public sealed class BedrockBone
{
    /// <summary>Unique bone name within the geometry (e.g. <c>"root"</c>, <c>"body"</c>, <c>"head"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional parent bone name. May appear before or after this bone in the JSON array.</summary>
    public string? Parent { get; set; }

    /// <summary>Bone rotation pivot in Bedrock model space <c>[x, y, z]</c>.</summary>
    public Vector3D Pivot { get; set; } = Vector3D.Zero;

    /// <summary>Bone rest/initial Euler rotation in degrees <c>[x, y, z]</c>.</summary>
    public Vector3D Rotation { get; set; } = Vector3D.Zero;

    /// <summary>Optional legacy bind pose rotation in degrees <c>[x, y, z]</c> (<c>bind_pose_rotation</c>).</summary>
    public Vector3D? BindPoseRotation { get; set; }

    /// <summary>Whether cubes attached to this bone mirror UVs by default.</summary>
    public bool? Mirror { get; set; }

    /// <summary>Default inflation in pixel units for cubes on this bone.</summary>
    public double? Inflate { get; set; }

    /// <summary>Whether debug visualization is enabled for this bone.</summary>
    public bool? Debug { get; set; }

    /// <summary>Optional render group index.</summary>
    public int? RenderGroupId { get; set; }

    /// <summary>Optional Molang bone binding expression (Bedrock 1.16.0+).</summary>
    public string? Binding { get; set; }

    /// <summary>Optional bone-level material name.</summary>
    public string? Material { get; set; }

    /// <summary>Optional legacy <c>neverRender</c> flag.</summary>
    public bool? NeverRender { get; set; }

    /// <summary>Optional legacy <c>reset</c> flag when inheriting from a parent geometry.</summary>
    public bool? Reset { get; set; }

    /// <summary>Cuboid primitives attached to this bone.</summary>
    public List<BedrockCube> Cubes { get; set; } = [];

    public JsonExtensionData ExtensionData { get; set; } = new();

    public BedrockBone DeepClone() => new()
    {
        Name = Name,
        Parent = Parent,
        Pivot = Pivot,
        Rotation = Rotation,
        BindPoseRotation = BindPoseRotation,
        Mirror = Mirror,
        Inflate = Inflate,
        Debug = Debug,
        RenderGroupId = RenderGroupId,
        Binding = Binding,
        Material = Material,
        NeverRender = NeverRender,
        Reset = Reset,
        Cubes = Cubes.Select(c => c.DeepClone()).ToList(),
        ExtensionData = ExtensionData.DeepClone()
    };
}

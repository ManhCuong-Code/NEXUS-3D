using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.VoxelJson.Models;

/// <summary>
/// Per-face UV and texture mapping for cuboid voxel elements (e.g., Blockbench / Minecraft Block Model JSON).
/// </summary>
public sealed class VoxelCuboidFaceUv
{
    /// <summary>UV rectangle <c>[u1, v1, u2, v2]</c> in texture coordinates.</summary>
    public double[]? Uv { get; set; }

    /// <summary>Texture reference key (e.g. <c>#0</c>, <c>#particle</c>, or material id).</summary>
    public string Texture { get; set; } = string.Empty;

    /// <summary>Optional cullface direction (<c>north</c>, <c>south</c>, <c>east</c>, <c>west</c>, <c>up</c>, <c>down</c>).</summary>
    public string? CullFace { get; set; }

    /// <summary>Optional texture rotation in degrees (<c>0</c>, <c>90</c>, <c>180</c>, <c>270</c>).</summary>
    public int? Rotation { get; set; }

    /// <summary>Optional tint index for biome/material tinting.</summary>
    public int? TintIndex { get; set; }

    public JsonExtensionData ExtensionData { get; set; } = new();

    public VoxelCuboidFaceUv DeepClone() => new()
    {
        Uv = Uv is not null ? [.. Uv] : null,
        Texture = Texture,
        CullFace = CullFace,
        Rotation = Rotation,
        TintIndex = TintIndex,
        ExtensionData = ExtensionData.DeepClone()
    };
}

/// <summary>
/// Represents a single voxel or cuboid element in a <see cref="VoxelDocument"/>.
/// Supports both discrete grid cells and non-uniform/sub-grid/rotated cuboids.
/// </summary>
public sealed class VoxelRecord
{
    /// <summary>Voxel lower-corner or grid cell position <c>(X, Y, Z)</c>.</summary>
    public Vector3D Position { get; set; }

    /// <summary>
    /// Optional explicit voxel dimensions <c>(Width, Height, Depth)</c>.
    /// When <see langword="null"/>, the grid's default <see cref="VoxelGridDefinition.VoxelSize"/> applies.
    /// </summary>
    public Vector3D? Size { get; set; }

    /// <summary>Optional Euler rotation in degrees <c>(Pitch, Yaw, Roll)</c>.</summary>
    public Vector3D? Rotation { get; set; }

    /// <summary>Optional rotation pivot/origin in model space (used by cuboid dialects).</summary>
    public Vector3D? RotationOrigin { get; set; }

    /// <summary>Optional primary rotation axis (<c>"x"</c>, <c>"y"</c>, <c>"z"</c>) when single-axis cuboid rotation is used.</summary>
    public string? RotationAxis { get; set; }

    /// <summary>Optional rescale flag for rotated cuboids.</summary>
    public bool? Rescale { get; set; }

    /// <summary>Optional directional shading flag for cuboids.</summary>
    public bool? Shade { get; set; }

    /// <summary>Direct RGBA color assigned to this voxel.</summary>
    public VoxelColor? Color { get; set; }

    /// <summary>Reference to a <see cref="VoxelMaterialReference.Id"/> in <see cref="VoxelDocument.Materials"/>.</summary>
    public string? MaterialId { get; set; }

    /// <summary>Logical voxel type or block identifier (e.g., <c>"solid"</c>, <c>"emissive"</c>, <c>"minecraft:stone"</c>).</summary>
    public string? VoxelType { get; set; }

    /// <summary>Reference to a <see cref="VoxelLayerDefinition.Id"/> in <see cref="VoxelDocument.Layers"/>.</summary>
    public string? LayerId { get; set; }

    /// <summary>Optional element or voxel name.</summary>
    public string? Name { get; set; }

    /// <summary>Optional per-face UV definitions for cuboid elements keyed by face direction (<c>north</c>, <c>south</c>, <c>east</c>, <c>west</c>, <c>up</c>, <c>down</c>).</summary>
    public Dictionary<string, VoxelCuboidFaceUv> Faces { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Custom string properties attached to the voxel.</summary>
    public Dictionary<string, string> CustomProperties { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Preserved unknown JSON properties on this voxel element.</summary>
    public JsonExtensionData ExtensionData { get; set; } = new();

    public Vector3D GetEffectiveSize(Vector3D defaultVoxelSize) => Size ?? defaultVoxelSize;

    public bool HasValidPosition => Position.IsFinite();

    public bool HasValidSize =>
        !Size.HasValue ||
        (Size.Value.IsFinite() &&
         Size.Value.X > 0.0 &&
         Size.Value.Y > 0.0 &&
         Size.Value.Z > 0.0);

    public VoxelRecord DeepClone()
    {
        var clone = new VoxelRecord
        {
            Position = Position,
            Size = Size,
            Rotation = Rotation,
            RotationOrigin = RotationOrigin,
            RotationAxis = RotationAxis,
            Rescale = Rescale,
            Shade = Shade,
            Color = Color,
            MaterialId = MaterialId,
            VoxelType = VoxelType,
            LayerId = LayerId,
            Name = Name,
            CustomProperties = new Dictionary<string, string>(CustomProperties, StringComparer.Ordinal),
            ExtensionData = ExtensionData.DeepClone()
        };

        foreach ((string faceKey, VoxelCuboidFaceUv faceUv) in Faces)
        {
            clone.Faces[faceKey] = faceUv.DeepClone();
        }

        return clone;
    }
}

using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.VoxelJson.Models;

/// <summary>
/// Defines the coordinate origin, cell resolution (<see cref="VoxelSize"/>), and bounding dimensions of a voxel grid.
/// Does not assume every voxel is 1x1x1.
/// </summary>
public sealed class VoxelGridDefinition
{
    /// <summary>Grid dimensions in voxel cells or world units (<c>[Width, Height, Depth]</c>).</summary>
    public Vector3D? Dimensions { get; set; }

    /// <summary>Grid origin in 3D space (<c>[X, Y, Z]</c>).</summary>
    public Vector3D Origin { get; set; } = Vector3D.Zero;

    /// <summary>Physical size of a single grid cell along X, Y, Z (default: <c>(1, 1, 1)</c>).</summary>
    public Vector3D VoxelSize { get; set; } = Vector3D.One;

    /// <summary>Texture atlas resolution (<c>[Width, Height]</c>, default <c>(16, 16)</c> for Minecraft cuboid models).</summary>
    public Vector2D TextureSize { get; set; } = new(16.0, 16.0);

    public JsonExtensionData ExtensionData { get; set; } = new();

    public bool HasValidVoxelSize =>
        VoxelSize.IsFinite() &&
        VoxelSize.X > 0.0 &&
        VoxelSize.Y > 0.0 &&
        VoxelSize.Z > 0.0;

    public bool HasValidDimensions =>
        !Dimensions.HasValue ||
        (Dimensions.Value.IsFinite() &&
         Dimensions.Value.X > 0.0 &&
         Dimensions.Value.Y > 0.0 &&
         Dimensions.Value.Z > 0.0);

    public VoxelGridDefinition DeepClone() => new()
    {
        Dimensions = Dimensions,
        Origin = Origin,
        VoxelSize = VoxelSize,
        TextureSize = TextureSize,
        ExtensionData = ExtensionData.DeepClone()
    };
}

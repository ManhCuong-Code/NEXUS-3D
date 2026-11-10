using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Editor.Domain;

/// <summary>
/// Parameters for UV coordinate modifications.
/// </summary>
public sealed record UVTransformSettings(
    Vector2D Offset,
    Vector2D Scale,
    double RotationDegrees,
    bool FlipHorizontal = false,
    bool FlipVertical = false)
{
    public static UVTransformSettings Identity => new(Vector2D.Zero, Vector2D.One, 0.0);
}


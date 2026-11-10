using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Service converting scene geometry and camera between handedness, up-axes, and scale conventions.
/// </summary>
public interface ICoordinateConverter
{
    void AdaptCoordinates(
        Scene3D scene,
        CoordinateSystem sourceCoords,
        CoordinateSystem targetCoords,
        CoordinateTransformOptions? options = null);
}

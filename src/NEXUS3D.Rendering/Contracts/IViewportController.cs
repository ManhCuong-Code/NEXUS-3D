using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.ValueObjects;

namespace NEXUS3D.Rendering.Contracts;

public enum ViewportShadingMode
{
    Solid = 0,
    Wireframe = 1,
    Textured = 2,
    VoxelGrid = 3
}

/// <summary>
/// Immutable snapshot of the 3D viewport configuration.
/// </summary>
public sealed record ViewportState(
    ViewportShadingMode ShadingMode,
    CoordinateAxisUp AxisUp,
    bool ShowGrid,
    bool ShowAxes,
    bool ShowBoundingBox,
    Vector3DValue CameraPosition,
    Vector3DValue CameraTarget,
    double FieldOfViewDegrees)
{
    public static ViewportState Default => new(
        ViewportShadingMode.Textured,
        CoordinateAxisUp.YUp,
        ShowGrid: true,
        ShowAxes: true,
        ShowBoundingBox: false,
        CameraPosition: new Vector3DValue(16, 16, 24),
        CameraTarget: Vector3DValue.Zero,
        FieldOfViewDegrees: 60.0);
}

/// <summary>
/// Foundational contract for the 3D viewport subsystem.
/// </summary>
public interface IViewportController
{
    ViewportState CurrentState { get; }
    void ResetCamera();
    void SetShadingMode(ViewportShadingMode mode);
    void ToggleGrid(bool isVisible);
}


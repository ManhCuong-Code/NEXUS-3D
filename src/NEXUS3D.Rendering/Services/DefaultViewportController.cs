using NEXUS3D.Rendering.Contracts;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Manages the 3D viewport state for the foundation UI before the hardware-accelerated renderer is attached.
/// </summary>
public sealed class DefaultViewportController : IViewportController
{
    public ViewportState CurrentState { get; private set; } = ViewportState.Default;

    public void ResetCamera()
    {
        CurrentState = ViewportState.Default with
        {
            ShadingMode = CurrentState.ShadingMode,
            ShowGrid = CurrentState.ShowGrid
        };
    }

    public void SetShadingMode(ViewportShadingMode mode)
    {
        CurrentState = CurrentState with { ShadingMode = mode };
    }

    public void ToggleGrid(bool isVisible)
    {
        CurrentState = CurrentState with { ShowGrid = isVisible };
    }
}


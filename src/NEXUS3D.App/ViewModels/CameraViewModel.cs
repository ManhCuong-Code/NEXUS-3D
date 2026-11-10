using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel coordinating camera modes, presets (Front, Back, Left, Right, Top, Bottom, Isometric),
/// perspective/orthographic toggle, and framing focus.
/// </summary>
public sealed partial class CameraViewModel : ObservableObject
{
    private readonly ICameraService _cameraService;

    [ObservableProperty]
    private CameraState _currentCamera = CameraState.Default;

    [ObservableProperty]
    private bool _isOrthographic;

    [ObservableProperty]
    private float _fieldOfView = 45f;

    public event EventHandler<CameraState>? CameraChanged;

    public CameraViewModel(ICameraService cameraService)
    {
        _cameraService = cameraService;
        _currentCamera = _cameraService.CurrentCamera;
    }

    [RelayCommand]
    public void SetPreset(CameraPresetView preset)
    {
        var target = CurrentCamera.Target;
        float dist = CurrentCamera.DistanceToTarget;
        var newCam = _cameraService.ApplyPreset(preset, target, dist);
        UpdateCamera(newCam);
    }

    public void Focus(Vector3 center, float radius)
    {
        var min = center - Vector3.One * Math.Max(0.5f, radius);
        var max = center + Vector3.One * Math.Max(0.5f, radius);
        var newCam = _cameraService.FocusBounds(min, max, FieldOfView);
        UpdateCamera(newCam);
    }

    [RelayCommand]
    public void ToggleProjection()
    {
        IsOrthographic = !IsOrthographic;
        var proj = IsOrthographic ? CameraProjectionType.Orthographic : CameraProjectionType.Perspective;
        var newCam = CurrentCamera with { ProjectionType = proj };
        UpdateCamera(newCam);
    }

    [RelayCommand]
    public void ResetCamera()
    {
        var newCam = _cameraService.ApplyPreset(CameraPresetView.Reset, Vector3.Zero, 20f);
        UpdateCamera(newCam);
    }

    private void UpdateCamera(CameraState newCam)
    {
        CurrentCamera = newCam;
        _cameraService.SetCamera(newCam);
        CameraChanged?.Invoke(this, newCam);
    }
}

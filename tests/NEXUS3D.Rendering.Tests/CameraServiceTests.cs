using System.Numerics;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class CameraServiceTests
{
    private readonly CameraService _cameraService = new();

    [Fact]
    public void Orbit_UpdatesPositionAroundTarget()
    {
        var initial = CameraState.Default;

        var updated = _cameraService.Orbit(initial, 45f, 30f);

        Assert.NotEqual(initial.Position, updated.Position);
        Assert.Equal(initial.Target, updated.Target);
    }

    [Fact]
    public void Pan_TranslatesTargetAndPositionAlongCameraAxes()
    {
        var initial = CameraState.Default with { Target = Vector3.Zero, Position = new Vector3(0, 0, 10) };

        var panned = _cameraService.Pan(initial, 2.0f, 3.0f);

        Assert.NotEqual(initial.Target, panned.Target);
        Assert.NotEqual(initial.Position, panned.Position);
        Assert.Equal(
            Vector3.Distance(initial.Position, initial.Target),
            Vector3.Distance(panned.Position, panned.Target),
            precision: 3);
    }

    [Fact]
    public void Zoom_ScalesDistanceWithinBounds()
    {
        var initial = CameraState.Default with { Target = Vector3.Zero, Position = new Vector3(0, 0, 10) };

        var zoomedIn = _cameraService.Zoom(initial, 0.5f);
        var distIn = Vector3.Distance(zoomedIn.Position, zoomedIn.Target);
        Assert.True(distIn < 10.0f);

        var zoomedOut = _cameraService.Zoom(initial, 1.5f);
        var distOut = Vector3.Distance(zoomedOut.Position, zoomedOut.Target);
        Assert.True(distOut > 10.0f);
    }

    [Theory]
    [InlineData(CameraPresetView.Front)]
    [InlineData(CameraPresetView.Back)]
    [InlineData(CameraPresetView.Left)]
    [InlineData(CameraPresetView.Right)]
    [InlineData(CameraPresetView.Top)]
    [InlineData(CameraPresetView.Bottom)]
    [InlineData(CameraPresetView.Isometric)]
    public void ApplyPreset_ProducesValidCameraState(CameraPresetView preset)
    {
        var result = _cameraService.ApplyPreset(preset, Vector3.Zero, 15f);

        Assert.NotNull(result);
        Assert.NotEqual(result.Position, result.Target);
        Assert.Equal(Vector3.Zero, result.Target);
    }

    [Fact]
    public void FocusBounds_CentersCameraOnBoundingBox()
    {
        var min = new Vector3(-5, -5, -5);
        var max = new Vector3(5, 5, 5);

        var focused = _cameraService.FocusBounds(min, max);

        Assert.Equal(Vector3.Zero, focused.Target);
        Assert.True(Vector3.Distance(focused.Position, focused.Target) >= 10f);
    }
}

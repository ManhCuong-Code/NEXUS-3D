using System.Numerics;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Professional camera service managing navigation, presets, orbit/pan/zoom math, and framing.
/// </summary>
public sealed class CameraService : ICameraService
{
    public CameraState CurrentCamera { get; private set; } = CameraState.Default;

    public void SetCamera(CameraState camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        CurrentCamera = camera;
    }

    public CameraState FocusBounds(Vector3 boundsMin, Vector3 boundsMax, float fieldOfViewDegrees = 45f)
    {
        var center = (boundsMin + boundsMax) * 0.5f;
        var radius = Math.Max(0.5f, Vector3.Distance(boundsMin, boundsMax) * 0.5f);

        float fovRad = fieldOfViewDegrees * (float)Math.PI / 180f;
        float distance = (radius / (float)Math.Sin(fovRad * 0.5f)) * 1.35f;
        distance = Math.Clamp(distance, 1.0f, 10000f);

        // Place camera at elevated isometric viewpoint
        var dir = Vector3.Normalize(new Vector3(1.2f, 1.0f, 1.4f));
        var pos = center + dir * distance;

        CurrentCamera = new CameraState(
            Position: pos,
            Target: center,
            Up: Vector3.UnitY,
            ProjectionType: CameraProjectionType.Perspective,
            FieldOfView: fieldOfViewDegrees,
            NearPlane: Math.Max(0.01f, distance * 0.001f),
            FarPlane: Math.Max(100f, distance * 10f),
            OrthographicWidth: radius * 2.5f);

        return CurrentCamera;
    }

    public CameraState ApplyPreset(CameraPresetView preset, Vector3 target, float distance)
    {
        distance = Math.Max(0.5f, distance);
        Vector3 pos;
        Vector3 up = Vector3.UnitY;

        switch (preset)
        {
            case CameraPresetView.Front:
                pos = target + new Vector3(0, 0, distance);
                break;
            case CameraPresetView.Back:
                pos = target + new Vector3(0, 0, -distance);
                break;
            case CameraPresetView.Left:
                pos = target + new Vector3(-distance, 0, 0);
                break;
            case CameraPresetView.Right:
                pos = target + new Vector3(distance, 0, 0);
                break;
            case CameraPresetView.Top:
                pos = target + new Vector3(0, distance, 0);
                up = -Vector3.UnitZ;
                break;
            case CameraPresetView.Bottom:
                pos = target + new Vector3(0, -distance, 0);
                up = Vector3.UnitZ;
                break;
            case CameraPresetView.Isometric:
                var isoDir = Vector3.Normalize(new Vector3(1, 1, 1));
                pos = target + isoDir * distance;
                break;
            case CameraPresetView.Reset:
            default:
                var defDir = Vector3.Normalize(new Vector3(1.2f, 1.0f, 1.4f));
                pos = target + defDir * distance;
                break;
        }

        CurrentCamera = CurrentCamera with
        {
            Position = pos,
            Target = target,
            Up = up,
            OrthographicWidth = distance * 1.5f
        };

        return CurrentCamera;
    }

    public CameraState Orbit(CameraState current, float deltaYawDegrees, float deltaPitchDegrees)
    {
        var offset = current.Position - current.Target;
        float radius = Math.Max(0.01f, offset.Length());

        // Convert to spherical coords
        float yaw = (float)Math.Atan2(offset.X, offset.Z);
        float pitch = (float)Math.Asin(Math.Clamp(offset.Y / radius, -0.999f, 0.999f));

        yaw += deltaYawDegrees * ((float)Math.PI / 180f);
        pitch += deltaPitchDegrees * ((float)Math.PI / 180f);

        // Clamp pitch to avoid gimbal flip (-85 to +85 deg)
        float maxPitch = 85f * ((float)Math.PI / 180f);
        pitch = Math.Clamp(pitch, -maxPitch, maxPitch);

        float cosPitch = (float)Math.Cos(pitch);
        var newOffset = new Vector3(
            radius * cosPitch * (float)Math.Sin(yaw),
            radius * (float)Math.Sin(pitch),
            radius * cosPitch * (float)Math.Cos(yaw));

        CurrentCamera = current with
        {
            Position = current.Target + newOffset,
            Up = Vector3.UnitY
        };

        return CurrentCamera;
    }

    public CameraState Pan(CameraState current, float deltaX, float deltaY)
    {
        var forward = Vector3.Normalize(current.Target - current.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, current.Up));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));

        float speedFactor = current.DistanceToTarget * 0.002f;
        var panVector = (-right * deltaX + up * deltaY) * speedFactor;

        CurrentCamera = current with
        {
            Position = current.Position + panVector,
            Target = current.Target + panVector
        };

        return CurrentCamera;
    }

    public CameraState Zoom(CameraState current, float factor)
    {
        if (factor <= 0f)
        {
            return current;
        }

        var offset = current.Position - current.Target;
        float currentDistance = offset.Length();
        float newDistance = Math.Clamp(currentDistance * factor, 0.05f, 10000f);

        var newPos = current.Target + Vector3.Normalize(offset) * newDistance;
        float newOrtho = Math.Clamp(current.OrthographicWidth * factor, 0.1f, 10000f);

        CurrentCamera = current with
        {
            Position = newPos,
            OrthographicWidth = newOrtho
        };

        return CurrentCamera;
    }
}

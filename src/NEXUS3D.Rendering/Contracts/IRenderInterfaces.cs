using System.Numerics;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Contracts;

/// <summary>
/// Converts Universal Scene Model (USM Scene3D) into GPU-ready RenderScene.
/// </summary>
public interface IRenderSceneAdapter
{
    RenderScene Adapt(Scene3D scene, RenderSettings settings);
}

/// <summary>
/// Provides camera controls, navigation presets, and viewport framing.
/// </summary>
public interface ICameraService
{
    CameraState CurrentCamera { get; }
    void SetCamera(CameraState camera);
    CameraState FocusBounds(Vector3 boundsMin, Vector3 boundsMax, float fieldOfViewDegrees = 45f);
    CameraState ApplyPreset(CameraPresetView preset, Vector3 target, float distance);
    CameraState Orbit(CameraState current, float deltaYawDegrees, float deltaPitchDegrees);
    CameraState Pan(CameraState current, float deltaX, float deltaY);
    CameraState Zoom(CameraState current, float factor);
}

/// <summary>
/// Computes tight world-space bounding boxes for scene nodes and meshes.
/// </summary>
public interface IBoundingBoxCalculator
{
    (Vector3 Min, Vector3 Max) CalculateMeshBounds(Vector3[] positions, Matrix4x4 transform);
    (Vector3 Min, Vector3 Max) CalculateNodeBounds(RenderNode node);
    (Vector3 Min, Vector3 Max) CalculateSceneBounds(RenderScene scene);
}

/// <summary>
/// Adapts USM MaterialData to RenderMaterial.
/// </summary>
public interface IMaterialRenderAdapter
{
    RenderMaterial AdaptMaterial(MaterialData material, string? baseDirectory = null);
    RenderMaterial GetDefaultMaterial();
}

/// <summary>
/// Resolves and loads textures safely preventing path traversal.
/// </summary>
public interface ITextureLoader
{
    string? ResolveTexturePath(string textureRef, string? baseDirectory = null);
    bool IsTextureAvailable(string texturePath);
    (int Width, int Height) GetTextureDimensions(string texturePath);
}

/// <summary>
/// Manages and tracks GPU resources, buffers, and cached textures for safe disposal.
/// </summary>
public interface IRenderResourceManager : IDisposable
{
    void TrackResource(string key, object resource);
    bool TryGetResource<T>(string key, out T? resource) where T : class;
    void ReleaseResource(string key);
    void ClearAll();
    int ActiveResourceCount { get; }
}

/// <summary>
/// Core rendering pipeline service.
/// </summary>
public interface IRenderService
{
    RenderScene? CurrentScene { get; }
    RenderSettings Settings { get; set; }
    CameraState Camera { get; set; }
    SelectionInfo CurrentSelection { get; }

    Task<RenderScene> LoadSceneAsync(Scene3D scene, CancellationToken cancellationToken = default);
    void UpdateSettings(RenderSettings settings);
    void SelectNode(string? nodeId);
    void SelectBone(string? boneName);
    void ClearSelection();
}

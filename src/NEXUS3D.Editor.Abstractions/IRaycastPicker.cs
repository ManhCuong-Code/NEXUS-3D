using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Editor.Abstractions;

/// <summary>
/// Detailed hit test result from picking against 3D scene elements.
/// </summary>
public sealed record RaycastHitResult(
    Guid NodeId,
    Guid? MeshId,
    double Distance,
    Vector3D HitPoint,
    Vector3D Normal,
    int TriangleIndex);

/// <summary>
/// Raycasting service for picking objects and triangles in the 3D scene.
/// </summary>
public interface IRaycastPicker
{
    /// <summary>
    /// Casts a ray into the scene graph and returns the closest intersecting scene element.
    /// </summary>
    RaycastHitResult? Pick(Scene3D scene, Ray3D ray);

    /// <summary>
    /// Casts a ray into the scene graph and returns all intersecting hits sorted by ascending distance.
    /// </summary>
    IReadOnlyList<RaycastHitResult> PickAll(Scene3D scene, Ray3D ray);
}


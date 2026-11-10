using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Editor.Abstractions;

/// <summary>
/// Service managing pivot editing and positioning for SceneNodes without altering their visual world appearance.
/// </summary>
public interface IPivotService
{
    /// <summary>
    /// Gets the current effective pivot of the scene node in local or world coordinates.
    /// </summary>
    Vector3D GetPivot(SceneNode node, bool worldSpace = false);

    /// <summary>
    /// Sets a new pivot for the scene node.
    /// When <paramref name="preserveVisualAppearance"/> is true, geometry and children offsets
    /// are compensated so that rendered world positions remain invariant.
    /// </summary>
    void SetPivot(SceneNode node, Vector3D newPivot, bool preserveVisualAppearance = true);

    /// <summary>
    /// Repositions pivot to the geometry bounding box center of the node.
    /// </summary>
    void CenterPivot(SceneNode node);

    /// <summary>
    /// Resets pivot to the origin (0, 0, 0).
    /// </summary>
    void PivotToOrigin(SceneNode node);

    /// <summary>
    /// Positions pivot to the world bounding box center of the entire node hierarchy.
    /// </summary>
    void PivotToBoundsCenter(SceneNode node);

    /// <summary>
    /// Positions pivot to the average center of a selection of nodes.
    /// </summary>
    void PivotToSelectionCenter(IReadOnlyList<SceneNode> nodes);
}


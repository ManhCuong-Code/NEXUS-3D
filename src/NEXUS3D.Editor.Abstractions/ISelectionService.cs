using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Editor.Abstractions;

/// <summary>
/// Service managing interactive selection of SceneNodes and Bones in the editor.
/// </summary>
public interface ISelectionService
{
    /// <summary>
    /// Set of currently selected scene node IDs.
    /// </summary>
    IReadOnlySet<Guid> SelectedNodeIds { get; }

    /// <summary>
    /// The primary / active selected node ID (the most recently selected node).
    /// </summary>
    Guid? PrimarySelectedNodeId { get; }

    /// <summary>
    /// Currently selected skeleton bone ID, if any.
    /// </summary>
    Guid? SelectedBoneId { get; }

    /// <summary>
    /// Selects a node by ID. If <paramref name="multiSelect"/> is true, toggles or adds to current selection.
    /// </summary>
    void Select(Guid nodeId, bool multiSelect = false);

    /// <summary>
    /// Deselects a specific node by ID.
    /// </summary>
    void Deselect(Guid nodeId);

    /// <summary>
    /// Clears all selected nodes and bones.
    /// </summary>
    void ClearSelection();

    /// <summary>
    /// Selects an articulated bone by ID.
    /// </summary>
    void SelectBone(Guid? boneId);

    /// <summary>
    /// Determines whether the node with the given ID is selected.
    /// </summary>
    bool IsSelected(Guid nodeId);

    /// <summary>
    /// Computes the combined bounding box of all selected nodes in world space.
    /// </summary>
    BoundingBox? GetSelectionWorldBounds(Scene3D scene);

    /// <summary>
    /// Computes the central pivot point of all selected nodes in world space.
    /// </summary>
    Vector3D? GetSelectionPivot(Scene3D scene);

    /// <summary>
    /// Fired when node or bone selection changes.
    /// </summary>
    event EventHandler SelectionChanged;
}


using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Editor.Domain;

/// <summary>
/// Encapsulates the immutable or snapshot state of scene editor selection.
/// </summary>
public sealed record SelectionState(
    IReadOnlySet<Guid> SelectedNodeIds,
    Guid? PrimaryNodeId,
    Guid? SelectedBoneId,
    BoundingBox? WorldBounds,
    Vector3D? Pivot)
{
    public static SelectionState Empty => new(
        new HashSet<Guid>(),
        null,
        null,
        null,
        null);

    public bool HasSelection => SelectedNodeIds.Count > 0 || SelectedBoneId.HasValue;
    public int Count => SelectedNodeIds.Count;
}


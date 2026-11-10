using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.Editor.Engine.Services;

/// <summary>
/// Implements interactive object and bone selection tracking with bounds and pivot calculation.
/// </summary>
public sealed class SelectionService : ISelectionService
{
    private readonly HashSet<Guid> _selectedNodeIds = [];

    public IReadOnlySet<Guid> SelectedNodeIds => _selectedNodeIds;
    public Guid? PrimarySelectedNodeId { get; private set; }
    public Guid? SelectedBoneId { get; private set; }

    public event EventHandler? SelectionChanged;

    public void Select(Guid nodeId, bool multiSelect = false)
    {
        if (nodeId == Guid.Empty)
        {
            ClearSelection();
            return;
        }

        if (multiSelect)
        {
            if (_selectedNodeIds.Contains(nodeId))
            {
                _selectedNodeIds.Remove(nodeId);
                if (PrimarySelectedNodeId == nodeId)
                {
                    PrimarySelectedNodeId = _selectedNodeIds.Count > 0 ? _selectedNodeIds.Last() : null;
                }
            }
            else
            {
                _selectedNodeIds.Add(nodeId);
                PrimarySelectedNodeId = nodeId;
            }
        }
        else
        {
            _selectedNodeIds.Clear();
            _selectedNodeIds.Add(nodeId);
            PrimarySelectedNodeId = nodeId;
        }

        // When node is selected, bone selection is cleared
        SelectedBoneId = null;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Deselect(Guid nodeId)
    {
        if (_selectedNodeIds.Remove(nodeId))
        {
            if (PrimarySelectedNodeId == nodeId)
            {
                PrimarySelectedNodeId = _selectedNodeIds.Count > 0 ? _selectedNodeIds.Last() : null;
            }
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearSelection()
    {
        if (_selectedNodeIds.Count > 0 || PrimarySelectedNodeId.HasValue || SelectedBoneId.HasValue)
        {
            _selectedNodeIds.Clear();
            PrimarySelectedNodeId = null;
            SelectedBoneId = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SelectBone(Guid? boneId)
    {
        if (SelectedBoneId != boneId)
        {
            _selectedNodeIds.Clear();
            PrimarySelectedNodeId = null;
            SelectedBoneId = boneId;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsSelected(Guid nodeId) => _selectedNodeIds.Contains(nodeId);

    public BoundingBox? GetSelectionWorldBounds(Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (_selectedNodeIds.Count == 0) return null;

        BoundingBox combined = BoundingBox.Empty;
        bool hasAny = false;

        foreach (var id in _selectedNodeIds)
        {
            var node = scene.Graph.FindNode(id);
            if (node?.GetWorldBounds() is { IsValid: true } bounds)
            {
                combined = combined.Encapsulate(bounds);
                hasAny = true;
            }
        }

        return hasAny && combined.IsValid ? combined : null;
    }

    public Vector3D? GetSelectionPivot(Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (_selectedNodeIds.Count == 0) return null;

        if (_selectedNodeIds.Count == 1 && PrimarySelectedNodeId.HasValue)
        {
            var singleNode = scene.Graph.FindNode(PrimarySelectedNodeId.Value);
            if (singleNode is not null)
            {
                return singleNode.GetWorldMatrix().Translation;
            }
        }

        var bounds = GetSelectionWorldBounds(scene);
        return bounds.HasValue ? (bounds.Value.Min + bounds.Value.Max) * 0.5 : null;
    }
}


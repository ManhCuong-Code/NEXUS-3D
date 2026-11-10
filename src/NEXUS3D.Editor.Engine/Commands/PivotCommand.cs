using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.Editor.Engine.Commands;

/// <summary>
/// Command recording pivot modification on a scene node, storing old and new vertex/transform compensations.
/// </summary>
public sealed class PivotCommand : IEditorCommand
{
    private readonly SceneNode _node;
    private readonly Vector3D _oldPivot;
    private readonly Vector3D _newPivot;
    private readonly Transform3D _oldTransform;
    private readonly Transform3D _newTransform;
    private readonly List<(MeshData Mesh, List<Vertex> Vertices)> _oldVertices = [];
    private readonly List<(MeshData Mesh, List<Vertex> Vertices)> _newVertices = [];
    private readonly List<(SceneNode Child, Transform3D Transform)> _oldChildTransforms = [];
    private readonly List<(SceneNode Child, Transform3D Transform)> _newChildTransforms = [];

    public string Name => "Edit Pivot";

    public PivotCommand(
        SceneNode node,
        Vector3D oldPivot,
        Vector3D newPivot,
        Transform3D oldTransform,
        Transform3D newTransform,
        IEnumerable<(MeshData Mesh, List<Vertex> OldVertices, List<Vertex> NewVertices)> meshChanges,
        IEnumerable<(SceneNode Child, Transform3D OldTransform, Transform3D NewTransform)> childChanges)
    {
        _node = node;
        _oldPivot = oldPivot;
        _newPivot = newPivot;
        _oldTransform = oldTransform.DeepClone();
        _newTransform = newTransform.DeepClone();

        foreach (var (m, oldV, newV) in meshChanges)
        {
            _oldVertices.Add((m, oldV.Select(v => v.DeepClone()).ToList()));
            _newVertices.Add((m, newV.Select(v => v.DeepClone()).ToList()));
        }

        foreach (var (c, oldT, newT) in childChanges)
        {
            _oldChildTransforms.Add((c, oldT.DeepClone()));
            _newChildTransforms.Add((c, newT.DeepClone()));
        }
    }

    public void Execute()
    {
        _node.LocalTransform = _newTransform.DeepClone();

        foreach (var (mesh, newV) in _newVertices)
        {
            mesh.Vertices = newV.Select(v => v.DeepClone()).ToList();
        }

        foreach (var (child, newT) in _newChildTransforms)
        {
            child.LocalTransform = newT.DeepClone();
        }
    }

    public void Undo()
    {
        _node.LocalTransform = _oldTransform.DeepClone();

        foreach (var (mesh, oldV) in _oldVertices)
        {
            mesh.Vertices = oldV.Select(v => v.DeepClone()).ToList();
        }

        foreach (var (child, oldT) in _oldChildTransforms)
        {
            child.LocalTransform = oldT.DeepClone();
        }
    }
}


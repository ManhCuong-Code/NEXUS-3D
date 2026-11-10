using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.Editor.Engine.Commands;

/// <summary>
/// Command recording transform changes for one or multiple scene nodes.
/// </summary>
public sealed class TransformCommand : IEditorCommand
{
    private readonly List<(SceneNode Node, Transform3D OldTransform, Transform3D NewTransform)> _nodeChanges;

    public string Name { get; }

    public TransformCommand(string name, IEnumerable<(SceneNode Node, Transform3D OldTransform, Transform3D NewTransform)> changes)
    {
        Name = name;
        _nodeChanges = changes.Select(c => (c.Node, c.OldTransform.DeepClone(), c.NewTransform.DeepClone())).ToList();
    }

    public TransformCommand(string name, SceneNode node, Transform3D oldTransform, Transform3D newTransform)
        : this(name, new[] { (node, oldTransform, newTransform) })
    {
    }

    public void Execute()
    {
        foreach (var (node, _, newTransform) in _nodeChanges)
        {
            node.LocalTransform = newTransform.DeepClone();
        }
    }

    public void Undo()
    {
        foreach (var (node, oldTransform, _) in _nodeChanges)
        {
            node.LocalTransform = oldTransform.DeepClone();
        }
    }
}


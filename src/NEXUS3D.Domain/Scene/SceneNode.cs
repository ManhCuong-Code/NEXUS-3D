using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Scene;

/// <summary>
/// Fundamental hierarchical unit in a <see cref="SceneGraph"/>.
/// Maintains local transform relative to parent and computes accumulated world transform and bounds.
/// </summary>
public sealed class SceneNode : Entity, ISceneComponent, IDeepCloneable<SceneNode>
{
    private readonly List<SceneNode> _children = [];

    public string Name { get; set; } = "Node";
    public Guid? ParentId { get; internal set; }
    public Transform3D LocalTransform { get; set; } = Transform3D.Identity();
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public SceneObject? Object { get; set; }

    internal SceneNode? ParentNode { get; set; }
    internal SceneGraph? OwningGraph { get; set; }

    public IReadOnlyCollection<SceneNode> Children => _children;
    internal List<SceneNode> InternalChildren => _children;

    public SceneNode()
    {
    }

    public SceneNode(string name, SceneObject? sceneObject = null)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
        Object = sceneObject;
    }

    public Matrix4x4D GetWorldMatrix()
    {
        Matrix4x4D current = LocalTransform.ToMatrix();
        var visited = new HashSet<Guid> { Id };
        SceneNode? parent = ParentNode;

        while (parent is not null)
        {
            if (!visited.Add(parent.Id))
            {
                // Cycle guard
                break;
            }

            current = parent.LocalTransform.ToMatrix() * current;
            parent = parent.ParentNode;
        }

        return current;
    }

    public BoundingBox? GetWorldBounds()
    {
        BoundingBox combined = BoundingBox.Empty;
        var visited = new HashSet<Guid>();
        var stack = new Stack<SceneNode>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            SceneNode node = stack.Pop();
            if (!visited.Add(node.Id))
            {
                continue;
            }

            if (node.Object?.GetLocalBounds() is { IsValid: true } localBounds)
            {
                BoundingBox transformed = localBounds.Transform(node.GetWorldMatrix());
                combined = combined.Encapsulate(transformed);
            }

            for (int i = 0; i < node._children.Count; i++)
            {
                stack.Push(node._children[i]);
            }
        }

        return combined.IsValid ? combined : null;
    }

    public void AddChild(SceneNode child)
    {
        DomainGuard.NotNull(child);

        if (OwningGraph is not null)
        {
            if (!OwningGraph.AddNode(this, child))
            {
                throw new DomainException(
                    UsmErrorCodes.InvalidScene,
                    $"Cannot add child node '{child.Name}' ({child.Id}) to parent '{Name}' ({Id}).");
            }

            return;
        }

        if (child.Id == Id || ReferenceEquals(child, this))
        {
            throw new DomainException(UsmErrorCodes.SceneGraphCycle, "A SceneNode cannot be a child of itself.");
        }

        if (child.ParentNode is not null || child.ParentId.HasValue)
        {
            throw new DomainException(UsmErrorCodes.InvalidScene, "Child node already belongs to a parent node.");
        }

        if (_children.Any(c => c.Id == child.Id))
        {
            throw new DomainException(UsmErrorCodes.DuplicateId, $"Duplicate child node ID '{child.Id}'.");
        }

        child.ParentId = Id;
        child.ParentNode = this;
        _children.Add(child);
    }

    public bool RemoveChild(Guid childId)
    {
        if (OwningGraph is not null)
        {
            var target = _children.FirstOrDefault(c => c.Id == childId);
            return target is not null && OwningGraph.RemoveNode(childId);
        }

        int idx = _children.FindIndex(c => c.Id == childId);
        if (idx < 0)
        {
            return false;
        }

        SceneNode removed = _children[idx];
        removed.ParentId = null;
        removed.ParentNode = null;
        _children.RemoveAt(idx);
        return true;
    }

    public SceneNode DeepClone()
    {
        var rootClone = new SceneNode(Name)
        {
            Id = Id,
            ParentId = ParentId,
            LocalTransform = LocalTransform.DeepClone(),
            IsVisible = IsVisible,
            IsLocked = IsLocked,
            Object = Object?.DeepClone()
        };

        var stack = new Stack<(SceneNode Source, SceneNode Target)>();
        stack.Push((this, rootClone));

        while (stack.Count > 0)
        {
            var (source, target) = stack.Pop();
            for (int i = 0; i < source._children.Count; i++)
            {
                SceneNode child = source._children[i];
                var clonedChild = new SceneNode(child.Name)
                {
                    Id = child.Id,
                    ParentId = target.Id,
                    ParentNode = target,
                    LocalTransform = child.LocalTransform.DeepClone(),
                    IsVisible = child.IsVisible,
                    IsLocked = child.IsLocked,
                    Object = child.Object?.DeepClone()
                };
                target._children.Add(clonedChild);
                stack.Push((child, clonedChild));
            }
        }

        return rootClone;
    }
}

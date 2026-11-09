using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Domain.Scene;

/// <summary>
/// Manages the hierarchical tree of <see cref="SceneNode"/> instances within a <see cref="Scene3D"/>.
/// Enforces single-parent invariants, unique node IDs, and non-recursive depth-first traversal for large scenes.
/// </summary>
public sealed class SceneGraph : IDeepCloneable<SceneGraph>
{
    private readonly Dictionary<Guid, SceneNode> _nodeIndex = [];

    public SceneNode Root { get; private set; }

    public IReadOnlyCollection<SceneNode> Nodes => _nodeIndex.Values;

    public SceneGraph()
        : this(new SceneNode("Root"))
    {
    }

    public SceneGraph(SceneNode root)
    {
        DomainGuard.NotNull(root);
        Root = root;
        Root.ParentId = null;
        Root.ParentNode = null;
        RebuildIndexAndBindHierarchy();
    }

    public static SceneGraph CreateDefault() => new(new SceneNode("Root"));

    public SceneNode? FindNode(Guid id) =>
        _nodeIndex.GetValueOrDefault(id);

    public bool AddNode(SceneNode parent, SceneNode child)
    {
        if (parent is null || child is null)
        {
            return false;
        }

        if (!_nodeIndex.ContainsKey(parent.Id))
        {
            return false;
        }

        if (parent.Id == child.Id || ReferenceEquals(parent, child))
        {
            return false;
        }

        // Reject if child already has a parent or belongs to another SceneGraph
        if (child.ParentNode is not null ||
            child.ParentId.HasValue ||
            (child.OwningGraph is not null && !ReferenceEquals(child.OwningGraph, this)))
        {
            return false;
        }

        // Collect all nodes in child's subtree iteratively and verify no duplicate IDs or cycles
        var subtreeNodes = new List<SceneNode>();
        var subtreeIds = new HashSet<Guid>();
        var stack = new Stack<SceneNode>();
        stack.Push(child);

        while (stack.Count > 0)
        {
            SceneNode current = stack.Pop();
            if (current.Id == Guid.Empty ||
                !subtreeIds.Add(current.Id) ||
                _nodeIndex.ContainsKey(current.Id))
            {
                return false;
            }

            if (current.OwningGraph is not null && !ReferenceEquals(current.OwningGraph, this))
            {
                return false;
            }

            subtreeNodes.Add(current);

            for (int i = current.InternalChildren.Count - 1; i >= 0; i--)
            {
                SceneNode subChild = current.InternalChildren[i];
                subChild.ParentId = current.Id;
                subChild.ParentNode = current;
                stack.Push(subChild);
            }
        }

        child.ParentId = parent.Id;
        child.ParentNode = parent;
        parent.InternalChildren.Add(child);

        for (int i = 0; i < subtreeNodes.Count; i++)
        {
            SceneNode n = subtreeNodes[i];
            n.OwningGraph = this;
            _nodeIndex[n.Id] = n;
        }

        return true;
    }

    public bool RemoveNode(Guid nodeId)
    {
        if (nodeId == Root.Id || !_nodeIndex.TryGetValue(nodeId, out SceneNode? target))
        {
            return false;
        }

        if (target.ParentNode is not null)
        {
            target.ParentNode.InternalChildren.Remove(target);
        }

        target.ParentId = null;
        target.ParentNode = null;

        // Iteratively detach target and all its descendants from this SceneGraph
        var stack = new Stack<SceneNode>();
        stack.Push(target);

        while (stack.Count > 0)
        {
            SceneNode current = stack.Pop();
            current.OwningGraph = null;
            _nodeIndex.Remove(current.Id);

            for (int i = 0; i < current.InternalChildren.Count; i++)
            {
                stack.Push(current.InternalChildren[i]);
            }
        }

        return true;
    }

    public bool ReparentNode(Guid nodeId, Guid newParentId, bool preserveWorldPosition = false)
    {
        if (nodeId == Root.Id || nodeId == newParentId)
        {
            return false;
        }

        if (!_nodeIndex.TryGetValue(nodeId, out SceneNode? node) ||
            !_nodeIndex.TryGetValue(newParentId, out SceneNode? newParent))
        {
            return false;
        }

        // Prevent cycles: newParent cannot be a descendant of node
        SceneNode? cursor = newParent;
        var visited = new HashSet<Guid>();
        while (cursor is not null)
        {
            if (cursor.Id == nodeId || !visited.Add(cursor.Id))
            {
                return false;
            }

            cursor = cursor.ParentNode;
        }

        Vector3D worldPosBefore = preserveWorldPosition
            ? node.GetWorldMatrix().Translation
            : Vector3D.Zero;

        node.ParentNode?.InternalChildren.Remove(node);
        node.ParentId = newParent.Id;
        node.ParentNode = newParent;
        newParent.InternalChildren.Add(node);

        if (preserveWorldPosition)
        {
            Matrix4x4D parentWorld = newParent.GetWorldMatrix();
            if (parentWorld.TryInvert(out Matrix4x4D invParentWorld))
            {
                node.LocalTransform.Position = invParentWorld.TransformPoint(worldPosBefore);
            }
        }

        return true;
    }

    /// <summary>
    /// Performs an iterative pre-order depth-first traversal of the scene graph.
    /// Safe for 100,000+ nodes without risk of StackOverflowException.
    /// </summary>
    public IEnumerable<SceneNode> Traverse()
    {
        var visited = new HashSet<Guid>();
        var stack = new Stack<SceneNode>();
        stack.Push(Root);

        while (stack.Count > 0)
        {
            SceneNode current = stack.Pop();
            if (!visited.Add(current.Id))
            {
                continue;
            }

            yield return current;

            var children = current.InternalChildren;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }
    }

    public int ComputeMaxDepth()
    {
        int maxDepth = 0;
        var visited = new HashSet<Guid>();
        var stack = new Stack<(SceneNode Node, int Depth)>();
        stack.Push((Root, 1));

        while (stack.Count > 0)
        {
            var (current, depth) = stack.Pop();
            if (!visited.Add(current.Id))
            {
                continue;
            }

            if (depth > maxDepth)
            {
                maxDepth = depth;
            }

            var children = current.InternalChildren;
            for (int i = 0; i < children.Count; i++)
            {
                stack.Push((children[i], depth + 1));
            }
        }

        return maxDepth;
    }

    public bool ContainsCycle()
    {
        var visited = new HashSet<Guid>();
        var recursionStack = new HashSet<Guid>();
        var frameStack = new Stack<(SceneNode Node, int NextChildIndex)>();

        frameStack.Push((Root, 0));
        visited.Add(Root.Id);
        recursionStack.Add(Root.Id);

        while (frameStack.Count > 0)
        {
            var (current, nextIdx) = frameStack.Pop();
            var children = current.InternalChildren;

            if (nextIdx < children.Count)
            {
                frameStack.Push((current, nextIdx + 1));
                SceneNode child = children[nextIdx];

                if (recursionStack.Contains(child.Id))
                {
                    return true;
                }

                if (visited.Add(child.Id))
                {
                    recursionStack.Add(child.Id);
                    frameStack.Push((child, 0));
                }
            }
            else
            {
                recursionStack.Remove(current.Id);
            }
        }

        return false;
    }

    public ValidationResult Validate()
    {
        var result = new ValidationResult();

        if (Root is null)
        {
            result.AddError(UsmErrorCodes.InvalidScene, "SceneGraph Root node cannot be null.", "Graph.Root");
            return result;
        }

        if (Root.ParentId.HasValue || Root.ParentNode is not null)
        {
            result.AddError(UsmErrorCodes.InvalidScene, "Root node must not have a parent.", "Graph.Root.ParentId", Root.Id);
        }

        if (ContainsCycle())
        {
            result.AddError(UsmErrorCodes.SceneGraphCycle, "SceneGraph contains a parent-child cycle.", "Graph", Root.Id);
            return result;
        }

        var seenIds = new HashSet<Guid>();
        var stack = new Stack<(SceneNode Node, SceneNode? ExpectedParent)>();
        stack.Push((Root, null));

        while (stack.Count > 0)
        {
            var (node, expectedParent) = stack.Pop();

            if (node.Id == Guid.Empty)
            {
                result.AddError(UsmErrorCodes.InvalidScene, $"Node '{node.Name}' has an empty Guid.", "Graph.Nodes");
            }
            else if (!seenIds.Add(node.Id))
            {
                result.AddError(UsmErrorCodes.DuplicateId, $"Duplicate SceneNode ID '{node.Id}' detected.", "Graph.Nodes", node.Id);
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Name))
            {
                result.AddError(UsmErrorCodes.InvalidScene, "SceneNode name cannot be empty.", $"Node[{node.Id}].Name", node.Id);
            }

            if (expectedParent is not null && node.ParentId != expectedParent.Id)
            {
                result.AddError(
                    UsmErrorCodes.InvalidScene,
                    $"Node '{node.Name}' ParentId ({node.ParentId}) does not match actual parent ({expectedParent.Id}).",
                    $"Node[{node.Id}].ParentId",
                    node.Id);
            }

            if (!node.LocalTransform.IsValid)
            {
                result.AddError(
                    UsmErrorCodes.InvalidTransform,
                    $"Node '{node.Name}' has an invalid or zero-scale LocalTransform.",
                    $"Node[{node.Id}].LocalTransform",
                    node.Id);
            }
            else if (node.LocalTransform.HasNegativeScale)
            {
                result.AddWarning(
                    UsmErrorCodes.InvalidTransform,
                    $"Node '{node.Name}' uses negative scale, which reverses polygon winding order and surface normals.",
                    $"Node[{node.Id}].LocalTransform.Scale",
                    node.Id);
            }

            for (int i = node.InternalChildren.Count - 1; i >= 0; i--)
            {
                stack.Push((node.InternalChildren[i], node));
            }
        }

        return result;
    }

    internal void RebuildIndexAndBindHierarchy()
    {
        _nodeIndex.Clear();
        if (Root is null)
        {
            return;
        }

        Root.ParentId = null;
        Root.ParentNode = null;

        var visited = new HashSet<Guid>();
        var stack = new Stack<SceneNode>();
        stack.Push(Root);

        while (stack.Count > 0)
        {
            SceneNode current = stack.Pop();
            if (!visited.Add(current.Id))
            {
                continue;
            }

            current.OwningGraph = this;
            _nodeIndex[current.Id] = current;

            for (int i = current.InternalChildren.Count - 1; i >= 0; i--)
            {
                SceneNode child = current.InternalChildren[i];
                child.ParentId = current.Id;
                child.ParentNode = current;
                stack.Push(child);
            }
        }
    }

    public SceneGraph DeepClone()
    {
        SceneNode clonedRoot = Root.DeepClone();
        return new SceneGraph(clonedRoot);
    }
}


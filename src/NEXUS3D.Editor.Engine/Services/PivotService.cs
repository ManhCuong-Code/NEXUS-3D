using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.Editor.Engine.Services;

/// <summary>
/// Service managing pivot placement for scene nodes while mathematically preserving world geometry positions.
/// </summary>
public sealed class PivotService : IPivotService
{
    public Vector3D GetPivot(SceneNode node, bool worldSpace = false)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (worldSpace)
        {
            return node.GetWorldMatrix().Translation;
        }

        return Vector3D.Zero;
    }

    public void SetPivot(SceneNode node, Vector3D newPivot, bool preserveVisualAppearance = true)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (newPivot.LengthSquared() <= MathUtilities.Epsilon && !preserveVisualAppearance) return;

        var delta = newPivot; // Local shift from current (0,0,0) to newPivot

        if (preserveVisualAppearance)
        {
            // 1. Shift all mesh vertices by -delta
            if (node.Object is not null)
            {
                foreach (var mesh in node.Object.Meshes)
                {
                    for (int i = 0; i < mesh.Vertices.Count; i++)
                    {
                        mesh.Vertices[i].Position -= delta;
                    }
                }
            }

            // 2. Shift all children local positions by -delta
            foreach (var child in node.Children)
            {
                child.LocalTransform.Position -= delta;
            }

            // 3. Compensate node's local position: Node.Pos += R * (S * delta)
            var scaledDelta = new Vector3D(
                delta.X * node.LocalTransform.Scale.X,
                delta.Y * node.LocalTransform.Scale.Y,
                delta.Z * node.LocalTransform.Scale.Z);

            var worldCompensatedDelta = node.LocalTransform.Rotation.RotateVector(scaledDelta);
            node.LocalTransform.Position += worldCompensatedDelta;
        }
    }

    public void CenterPivot(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Object is not null && node.Object.Meshes.Count > 0)
        {
            BoundingBox localBounds = BoundingBox.Empty;
            foreach (var mesh in node.Object.Meshes)
            {
                localBounds = localBounds.Encapsulate(mesh.Bounds);
            }

            if (localBounds.IsValid)
            {
                var center = (localBounds.Min + localBounds.Max) * 0.5;
                SetPivot(node, center, preserveVisualAppearance: true);
            }
        }
    }

    public void PivotToOrigin(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Move pivot so that world origin (0,0,0) becomes the node's pivot
        if (node.GetWorldMatrix().TryInvert(out var invWorld))
        {
            var localOrigin = invWorld.TransformPoint(Vector3D.Zero);
            SetPivot(node, localOrigin, preserveVisualAppearance: true);
        }
    }

    public void PivotToBoundsCenter(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var worldBounds = node.GetWorldBounds();
        if (worldBounds is { IsValid: true } bounds && node.GetWorldMatrix().TryInvert(out var invWorld))
        {
            var worldCenter = (bounds.Min + bounds.Max) * 0.5;
            var localCenter = invWorld.TransformPoint(worldCenter);
            SetPivot(node, localCenter, preserveVisualAppearance: true);
        }
    }

    public void PivotToSelectionCenter(IReadOnlyList<SceneNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0) return;

        // Compute average center of all nodes in world space
        Vector3D sumWorld = Vector3D.Zero;
        int count = 0;

        foreach (var node in nodes)
        {
            sumWorld += node.GetWorldMatrix().Translation;
            count++;
        }

        if (count == 0) return;
        var avgWorld = sumWorld * (1.0 / count);

        foreach (var node in nodes)
        {
            if (node.GetWorldMatrix().TryInvert(out var invWorld))
            {
                var localTarget = invWorld.TransformPoint(avgWorld);
                SetPivot(node, localTarget, preserveVisualAppearance: true);
            }
        }
    }
}

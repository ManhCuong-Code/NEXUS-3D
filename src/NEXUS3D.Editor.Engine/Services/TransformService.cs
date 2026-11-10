using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Mathematics;

namespace NEXUS3D.Editor.Engine.Services;

/// <summary>
/// Implements interactive transformation algorithms for SceneNodes across World and Local coordinate systems.
/// </summary>
public sealed class TransformService : ITransformService
{
    public EditorToolType ActiveTool { get; set; } = EditorToolType.Select;
    public TransformSpace TransformSpace { get; set; } = TransformSpace.World;
    public bool SnappingEnabled { get; set; } = false;
    public double TranslationSnap { get; set; } = 0.5;
    public double RotationSnapDegrees { get; set; } = 15.0;
    public double ScaleSnap { get; set; } = 0.1;

    public void Translate(IReadOnlyList<SceneNode> nodes, Vector3D delta, TransformSpace space)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0 || delta.LengthSquared() <= MathUtilities.Epsilon) return;

        Vector3D appliedDelta = delta;
        if (SnappingEnabled && TranslationSnap > MathUtilities.Epsilon)
        {
            appliedDelta = new Vector3D(
                Math.Round(delta.X / TranslationSnap) * TranslationSnap,
                Math.Round(delta.Y / TranslationSnap) * TranslationSnap,
                Math.Round(delta.Z / TranslationSnap) * TranslationSnap);
        }

        foreach (var node in nodes)
        {
            if (node.IsLocked) continue;

            if (space == TransformSpace.Local)
            {
                var localOffset = node.LocalTransform.Rotation.RotateVector(appliedDelta);
                node.LocalTransform.Position += localOffset;
            }
            else
            {
                // World space translation
                if (node.ParentNode is not null && node.ParentNode.GetWorldMatrix().TryInvert(out var invParent))
                {
                    var parentSpaceDelta = invParent.TransformDirection(appliedDelta);
                    node.LocalTransform.Position += parentSpaceDelta;
                }
                else
                {
                    node.LocalTransform.Position += appliedDelta;
                }
            }
        }
    }

    public void Rotate(IReadOnlyList<SceneNode> nodes, QuaternionD rotationDelta, Vector3D pivot, TransformSpace space)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0 || rotationDelta.LengthSquared() <= MathUtilities.Epsilon) return;

        QuaternionD appliedRot = rotationDelta.Normalize();

        foreach (var node in nodes)
        {
            if (node.IsLocked) continue;

            var worldMatrix = node.GetWorldMatrix();
            var worldPos = worldMatrix.Translation;

            // Rotate world position around designated pivot
            var offsetFromPivot = worldPos - pivot;
            var newWorldPos = pivot + appliedRot.RotateVector(offsetFromPivot);

            if (space == TransformSpace.Local)
            {
                node.LocalTransform.Rotation = (node.LocalTransform.Rotation * appliedRot).Normalize();
            }
            else
            {
                // In world space, apply rotationDelta before parent/world
                if (node.ParentNode is not null && node.ParentNode.GetWorldMatrix().TryInvert(out var invParent))
                {
                    var (pPos, pRot, _) = TransformMath.Decompose(invParent);
                    node.LocalTransform.Rotation = (pRot * appliedRot * node.LocalTransform.Rotation).Normalize();
                }
                else
                {
                    node.LocalTransform.Rotation = (appliedRot * node.LocalTransform.Rotation).Normalize();
                }
            }

            // Update position in parent space
            if (node.ParentNode is not null && node.ParentNode.GetWorldMatrix().TryInvert(out var invParentPos))
            {
                node.LocalTransform.Position = invParentPos.TransformPoint(newWorldPos);
            }
            else
            {
                node.LocalTransform.Position = newWorldPos;
            }
        }
    }

    public void Scale(IReadOnlyList<SceneNode> nodes, Vector3D scaleMultiplier, Vector3D pivot)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0) return;

        // Guard against zero scale or collapsing
        double sx = Math.Abs(scaleMultiplier.X) < 0.001 ? 0.001 : scaleMultiplier.X;
        double sy = Math.Abs(scaleMultiplier.Y) < 0.001 ? 0.001 : scaleMultiplier.Y;
        double sz = Math.Abs(scaleMultiplier.Z) < 0.001 ? 0.001 : scaleMultiplier.Z;

        if (SnappingEnabled && ScaleSnap > MathUtilities.Epsilon)
        {
            sx = Math.Round(sx / ScaleSnap) * ScaleSnap;
            sy = Math.Round(sy / ScaleSnap) * ScaleSnap;
            sz = Math.Round(sz / ScaleSnap) * ScaleSnap;
            if (Math.Abs(sx) < 0.001) sx = 0.001;
            if (Math.Abs(sy) < 0.001) sy = 0.001;
            if (Math.Abs(sz) < 0.001) sz = 0.001;
        }

        var multiplier = new Vector3D(sx, sy, sz);

        foreach (var node in nodes)
        {
            if (node.IsLocked) continue;

            var worldPos = node.GetWorldMatrix().Translation;
            var offset = worldPos - pivot;
            var scaledOffset = new Vector3D(offset.X * multiplier.X, offset.Y * multiplier.Y, offset.Z * multiplier.Z);
            var newWorldPos = pivot + scaledOffset;

            // Apply position
            if (node.ParentNode is not null && node.ParentNode.GetWorldMatrix().TryInvert(out var invParent))
            {
                node.LocalTransform.Position = invParent.TransformPoint(newWorldPos);
            }
            else
            {
                node.LocalTransform.Position = newWorldPos;
            }

            // Apply scale
            var newScale = new Vector3D(
                node.LocalTransform.Scale.X * multiplier.X,
                node.LocalTransform.Scale.Y * multiplier.Y,
                node.LocalTransform.Scale.Z * multiplier.Z);

            if (Math.Abs(newScale.X) < 0.0001) newScale = new Vector3D(0.0001, newScale.Y, newScale.Z);
            if (Math.Abs(newScale.Y) < 0.0001) newScale = new Vector3D(newScale.X, 0.0001, newScale.Z);
            if (Math.Abs(newScale.Z) < 0.0001) newScale = new Vector3D(newScale.X, newScale.Y, 0.0001);

            node.LocalTransform.Scale = newScale;
        }
    }

    public void ResetTransform(IReadOnlyList<SceneNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        foreach (var node in nodes)
        {
            if (node.IsLocked) continue;
            node.LocalTransform = Transform3D.Identity();
        }
    }

    public void ApplyTransform(IReadOnlyList<SceneNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        foreach (var node in nodes)
        {
            if (node.IsLocked) continue;

            var matrix = node.LocalTransform.ToMatrix();

            // Bake into mesh vertices
            if (node.Object is not null)
            {
                foreach (var mesh in node.Object.Meshes)
                {
                    for (int i = 0; i < mesh.Vertices.Count; i++)
                    {
                        var v = mesh.Vertices[i];
                        v.Position = matrix.TransformPoint(v.Position);
                        if (v.Normal.HasValue)
                        {
                            v.Normal = matrix.TransformNormal(v.Normal.Value).Normalize();
                        }
                    }
                }
            }

            // Bake into child node local transforms
            foreach (var child in node.Children)
            {
                var combined = matrix * child.LocalTransform.ToMatrix();
                var (pos, rot, scale) = TransformMath.Decompose(combined);
                child.LocalTransform = new Transform3D(pos, rot, scale);
            }

            node.LocalTransform = Transform3D.Identity();
        }
    }
}

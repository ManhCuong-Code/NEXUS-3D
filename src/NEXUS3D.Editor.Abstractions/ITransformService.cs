using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Editor.Abstractions;

/// <summary>
/// Service managing transform operations (translate, rotate, scale, reset, bake/apply) on SceneNodes.
/// </summary>
public interface ITransformService
{
    /// <summary>
    /// Current active transform tool mode.
    /// </summary>
    EditorToolType ActiveTool { get; set; }

    /// <summary>
    /// Current active coordinate space (Local or World).
    /// </summary>
    TransformSpace TransformSpace { get; set; }

    /// <summary>
    /// Indicates whether snapping is enabled for transformations.
    /// </summary>
    bool SnappingEnabled { get; set; }

    /// <summary>
    /// Translation grid snap step (e.g. 0.25, 0.5, 1.0 units).
    /// </summary>
    double TranslationSnap { get; set; }

    /// <summary>
    /// Rotation snap angle in degrees (e.g. 15°, 30°, 45°, 90°).
    /// </summary>
    double RotationSnapDegrees { get; set; }

    /// <summary>
    /// Scale step snapping multiplier (e.g. 0.1).
    /// </summary>
    double ScaleSnap { get; set; }

    /// <summary>
    /// Translates the given scene nodes by a delta vector.
    /// </summary>
    void Translate(IReadOnlyList<SceneNode> nodes, Vector3D delta, TransformSpace space);

    /// <summary>
    /// Rotates the given scene nodes around a designated pivot by a rotation delta.
    /// </summary>
    void Rotate(IReadOnlyList<SceneNode> nodes, QuaternionD rotationDelta, Vector3D pivot, TransformSpace space);

    /// <summary>
    /// Scales the given scene nodes relative to a designated pivot.
    /// </summary>
    void Scale(IReadOnlyList<SceneNode> nodes, Vector3D scaleMultiplier, Vector3D pivot);

    /// <summary>
    /// Resets node transforms back to default identity (Pos: 0, Rot: Identity, Scale: 1).
    /// </summary>
    void ResetTransform(IReadOnlyList<SceneNode> nodes);

    /// <summary>
    /// Applies/bakes the current local transform directly into mesh vertices and child offsets,
    /// then resets the node's local transform to identity without changing visual appearance.
    /// </summary>
    void ApplyTransform(IReadOnlyList<SceneNode> nodes);
}


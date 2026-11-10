using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Skeletons;

namespace NEXUS3D.Editor.Abstractions;

/// <summary>
/// Service managing interactive bone manipulation, hierarchy parenting, and forward kinematics calculation.
/// </summary>
public interface IBoneEditorService
{
    /// <summary>
    /// Sets a new local transform for an articulated bone, preserving parent-child relationships.
    /// </summary>
    void SetBoneTransform(SkeletonData skeleton, Guid boneId, Transform3D newLocalTransform);

    /// <summary>
    /// Reparents a bone to a new parent bone (or root if null), guarding strictly against cyclic references.
    /// Returns true if reparenting succeeded.
    /// </summary>
    bool ReparentBone(SkeletonData skeleton, Guid boneId, Guid? newParentId);

    /// <summary>
    /// Adds a new bone to the skeleton with an optional parent and pivot point.
    /// </summary>
    BoneData AddBone(SkeletonData skeleton, string name, Guid? parentId, Vector3D pivot);

    /// <summary>
    /// Removes a bone from the skeleton and reparents or detaches its children.
    /// </summary>
    bool RemoveBone(SkeletonData skeleton, Guid boneId);

    /// <summary>
    /// Computes accumulated world transform matrices for all bones in the skeleton.
    /// </summary>
    Dictionary<Guid, Matrix4x4D> CalculateWorldTransforms(SkeletonData skeleton);
}


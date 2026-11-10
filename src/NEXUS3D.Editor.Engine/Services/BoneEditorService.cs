using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Mathematics;

namespace NEXUS3D.Editor.Engine.Services;

/// <summary>
/// Service managing interactive bone manipulation, hierarchy parenting, and forward kinematics calculation.
/// </summary>
public sealed class BoneEditorService : IBoneEditorService
{
    public void SetBoneTransform(SkeletonData skeleton, Guid boneId, Transform3D newLocalTransform)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(newLocalTransform);

        var bone = skeleton.FindBone(boneId);
        if (bone is null) return;

        bone.LocalTransform = newLocalTransform.DeepClone();
    }

    public bool ReparentBone(SkeletonData skeleton, Guid boneId, Guid? newParentId)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        var bone = skeleton.FindBone(boneId);
        if (bone is null) return false;

        // Cannot parent to self
        if (newParentId.HasValue && newParentId.Value == boneId)
        {
            return false;
        }

        // Cycle detection: ensure newParentId is not a descendant of boneId
        if (newParentId.HasValue && IsDescendantOf(skeleton, newParentId.Value, boneId))
        {
            return false;
        }

        var worldTransforms = CalculateWorldTransforms(skeleton);
        Matrix4x4D boneWorld = worldTransforms.TryGetValue(boneId, out var bw) ? bw : bone.LocalTransform.ToMatrix();

        // 1. Detach from current parent
        if (bone.ParentBoneId.HasValue)
        {
            var oldParent = skeleton.FindBone(bone.ParentBoneId.Value);
            oldParent?.ChildBoneIds.Remove(boneId);
        }

        // 2. Attach to new parent
        if (newParentId.HasValue)
        {
            var newParent = skeleton.FindBone(newParentId.Value);
            if (newParent is null) return false;

            if (!newParent.ChildBoneIds.Contains(boneId))
            {
                newParent.ChildBoneIds.Add(boneId);
            }

            bone.ParentBoneId = newParentId;

            // Recalculate local transform to preserve world pose
            if (worldTransforms.TryGetValue(newParentId.Value, out var parentWorld) && parentWorld.TryInvert(out var invParentWorld))
            {
                var newLocalMatrix = invParentWorld * boneWorld;
                var (pos, rot, scale) = TransformMath.Decompose(newLocalMatrix);
                bone.LocalTransform = new Transform3D(pos, rot, scale);
            }
        }
        else
        {
            // Promoting to root bone
            bone.ParentBoneId = null;
            var (pos, rot, scale) = TransformMath.Decompose(boneWorld);
            bone.LocalTransform = new Transform3D(pos, rot, scale);
        }

        return true;
    }

    public BoneData AddBone(SkeletonData skeleton, string name, Guid? parentId, Vector3D pivot)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        var bone = new BoneData(name, parentId)
        {
            Pivot = pivot,
            LocalTransform = Transform3D.Identity()
        };

        skeleton.Bones.Add(bone);

        if (parentId.HasValue)
        {
            var parent = skeleton.FindBone(parentId.Value);
            if (parent is not null && !parent.ChildBoneIds.Contains(bone.Id))
            {
                parent.ChildBoneIds.Add(bone.Id);
            }
        }
        else if (!skeleton.RootBoneId.HasValue)
        {
            skeleton.RootBoneId = bone.Id;
        }

        return bone;
    }

    public bool RemoveBone(SkeletonData skeleton, Guid boneId)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        var bone = skeleton.FindBone(boneId);
        if (bone is null) return false;

        // Reparent children to bone's parent
        foreach (var childId in bone.ChildBoneIds.ToList())
        {
            ReparentBone(skeleton, childId, bone.ParentBoneId);
        }

        if (bone.ParentBoneId.HasValue)
        {
            var parent = skeleton.FindBone(bone.ParentBoneId.Value);
            parent?.ChildBoneIds.Remove(boneId);
        }

        if (skeleton.RootBoneId == boneId)
        {
            skeleton.RootBoneId = bone.ChildBoneIds.Count > 0 ? bone.ChildBoneIds[0] : null;
        }

        return skeleton.Bones.Remove(bone);
    }

    public Dictionary<Guid, Matrix4x4D> CalculateWorldTransforms(SkeletonData skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        var result = new Dictionary<Guid, Matrix4x4D>();
        var boneMap = skeleton.Bones.ToDictionary(b => b.Id);

        // Find root bones (ParentBoneId is null or not found in map)
        var rootBones = skeleton.Bones.Where(b => !b.ParentBoneId.HasValue || !boneMap.ContainsKey(b.ParentBoneId.Value));

        foreach (var root in rootBones)
        {
            ComputeRecursive(root, Matrix4x4D.Identity, boneMap, result);
        }

        return result;
    }

    private static void ComputeRecursive(
        BoneData bone,
        Matrix4x4D parentWorldMatrix,
        Dictionary<Guid, BoneData> boneMap,
        Dictionary<Guid, Matrix4x4D> result)
    {
        // Bone world = ParentWorld * Translation(Pivot) * LocalTransform * Translation(-Pivot)
        var pivotTranslate = Matrix4x4D.CreateTranslation(bone.Pivot);
        var invPivotTranslate = Matrix4x4D.CreateTranslation(-bone.Pivot);

        var boneLocal = pivotTranslate * bone.LocalTransform.ToMatrix() * invPivotTranslate;
        var boneWorld = parentWorldMatrix * boneLocal;

        result[bone.Id] = boneWorld;

        foreach (var childId in bone.ChildBoneIds)
        {
            if (boneMap.TryGetValue(childId, out var child))
            {
                ComputeRecursive(child, boneWorld, boneMap, result);
            }
        }
    }

    private static bool IsDescendantOf(SkeletonData skeleton, Guid candidateId, Guid ancestorId)
    {
        var visited = new HashSet<Guid>();
        var current = skeleton.FindBone(candidateId);

        while (current is not null && current.ParentBoneId.HasValue)
        {
            if (!visited.Add(current.Id)) break;

            if (current.ParentBoneId.Value == ancestorId)
            {
                return true;
            }

            current = skeleton.FindBone(current.ParentBoneId.Value);
        }

        return false;
    }
}

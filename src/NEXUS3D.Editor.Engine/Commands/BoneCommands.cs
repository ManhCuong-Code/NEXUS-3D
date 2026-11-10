using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.Editor.Engine.Commands;

/// <summary>
/// Command recording transformation changes of a skeleton bone.
/// </summary>
public sealed class BoneTransformCommand : IEditorCommand
{
    private readonly BoneData _bone;
    private readonly Transform3D _oldTransform;
    private readonly Transform3D _newTransform;

    public string Name => $"Transform Bone '{_bone.Name}'";

    public BoneTransformCommand(BoneData bone, Transform3D oldTransform, Transform3D newTransform)
    {
        _bone = bone;
        _oldTransform = oldTransform.DeepClone();
        _newTransform = newTransform.DeepClone();
    }

    public void Execute()
    {
        _bone.LocalTransform = _newTransform.DeepClone();
    }

    public void Undo()
    {
        _bone.LocalTransform = _oldTransform.DeepClone();
    }
}

/// <summary>
/// Command recording bone parenting changes.
/// </summary>
public sealed class BoneReparentCommand : IEditorCommand
{
    private readonly SkeletonData _skeleton;
    private readonly BoneData _bone;
    private readonly Guid? _oldParentId;
    private readonly Guid? _newParentId;
    private readonly Transform3D _oldTransform;
    private readonly Transform3D _newTransform;

    public string Name => $"Reparent Bone '{_bone.Name}'";

    public BoneReparentCommand(
        SkeletonData skeleton,
        BoneData bone,
        Guid? oldParentId,
        Guid? newParentId,
        Transform3D oldTransform,
        Transform3D newTransform)
    {
        _skeleton = skeleton;
        _bone = bone;
        _oldParentId = oldParentId;
        _newParentId = newParentId;
        _oldTransform = oldTransform.DeepClone();
        _newTransform = newTransform.DeepClone();
    }

    public void Execute()
    {
        ApplyParenting(_oldParentId, _newParentId, _newTransform);
    }

    public void Undo()
    {
        ApplyParenting(_newParentId, _oldParentId, _oldTransform);
    }

    private void ApplyParenting(Guid? currentParentId, Guid? targetParentId, Transform3D targetTransform)
    {
        if (currentParentId.HasValue)
        {
            var oldParent = _skeleton.FindBone(currentParentId.Value);
            oldParent?.ChildBoneIds.Remove(_bone.Id);
        }

        if (targetParentId.HasValue)
        {
            var newParent = _skeleton.FindBone(targetParentId.Value);
            if (newParent is not null && !newParent.ChildBoneIds.Contains(_bone.Id))
            {
                newParent.ChildBoneIds.Add(_bone.Id);
            }
        }

        _bone.ParentBoneId = targetParentId;
        _bone.LocalTransform = targetTransform.DeepClone();
    }
}


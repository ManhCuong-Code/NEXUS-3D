using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Editor.Engine.Services;
using Xunit;

namespace NEXUS3D.Editor.Tests;

public sealed class BoneEditorServiceTests
{
    [Fact]
    public void ReparentBone_ValidParent_UpdatesHierarchy()
    {
        var service = new BoneEditorService();
        var skeleton = new SkeletonData("Skeleton");

        var root = service.AddBone(skeleton, "Root", null, Vector3D.Zero);
        var arm = service.AddBone(skeleton, "Arm", root.Id, new Vector3D(0, 10, 0));
        var hand = service.AddBone(skeleton, "Hand", null, new Vector3D(0, 20, 0));

        bool reparented = service.ReparentBone(skeleton, hand.Id, arm.Id);

        Assert.True(reparented);
        Assert.Equal(arm.Id, hand.ParentBoneId);
        Assert.Contains(hand.Id, arm.ChildBoneIds);
    }

    [Fact]
    public void ReparentBone_SelfParenting_Rejected()
    {
        var service = new BoneEditorService();
        var skeleton = new SkeletonData("Skeleton");
        var bone = service.AddBone(skeleton, "Root", null, Vector3D.Zero);

        bool reparented = service.ReparentBone(skeleton, bone.Id, bone.Id);

        Assert.False(reparented);
    }

    [Fact]
    public void ReparentBone_CycleDetection_Rejected()
    {
        var service = new BoneEditorService();
        var skeleton = new SkeletonData("Skeleton");

        var a = service.AddBone(skeleton, "A", null, Vector3D.Zero);
        var b = service.AddBone(skeleton, "B", a.Id, Vector3D.Zero);
        var c = service.AddBone(skeleton, "C", b.Id, Vector3D.Zero);

        // Attempting to parent A under C would create A -> B -> C -> A cycle!
        bool cycleResult = service.ReparentBone(skeleton, a.Id, c.Id);

        Assert.False(cycleResult);
        Assert.Null(a.ParentBoneId);
    }

    [Fact]
    public void RemoveBone_PromotesChildrenToParent()
    {
        var service = new BoneEditorService();
        var skeleton = new SkeletonData("Skeleton");

        var root = service.AddBone(skeleton, "Root", null, Vector3D.Zero);
        var mid = service.AddBone(skeleton, "Mid", root.Id, Vector3D.Zero);
        var tip = service.AddBone(skeleton, "Tip", mid.Id, Vector3D.Zero);

        bool removed = service.RemoveBone(skeleton, mid.Id);

        Assert.True(removed);
        Assert.DoesNotContain(mid, skeleton.Bones);
        Assert.Equal(root.Id, tip.ParentBoneId);
        Assert.Contains(tip.Id, root.ChildBoneIds);
    }

    [Fact]
    public void CalculateWorldTransforms_PropagatesParentTransforms()
    {
        var service = new BoneEditorService();
        var skeleton = new SkeletonData("Skeleton");

        var root = service.AddBone(skeleton, "Root", null, Vector3D.Zero);
        root.LocalTransform = new Transform3D(new Vector3D(0, 10, 0), QuaternionD.Identity, Vector3D.One);

        var child = service.AddBone(skeleton, "Child", root.Id, Vector3D.Zero);
        child.LocalTransform = new Transform3D(new Vector3D(5, 0, 0), QuaternionD.Identity, Vector3D.One);

        var worldTransforms = service.CalculateWorldTransforms(skeleton);

        Assert.True(worldTransforms.ContainsKey(root.Id));
        Assert.True(worldTransforms.ContainsKey(child.Id));

        var childWorldPos = worldTransforms[child.Id].Translation;
        Assert.Equal(new Vector3D(5, 10, 0), childWorldPos);
    }
}


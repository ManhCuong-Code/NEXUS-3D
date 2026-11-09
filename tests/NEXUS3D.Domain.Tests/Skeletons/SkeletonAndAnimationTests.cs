using NEXUS3D.Domain.Animations;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Skeletons;

namespace NEXUS3D.Domain.Tests.Skeletons;

public sealed class SkeletonAndAnimationTests
{
    [Fact]
    public void SkeletonData_CycleDetectionAndWeightNormalization_ShouldWorkCorrectly()
    {
        var skeleton = new SkeletonData("HumanoidRig");
        var rootBone = new BoneData("root");
        var spineBone = new BoneData("spine", rootBone.Id);
        var headBone = new BoneData("head", spineBone.Id);

        skeleton.RootBoneId = rootBone.Id;
        skeleton.Bones.AddRange([rootBone, spineBone, headBone]);

        var influence = new BoneInfluence
        {
            VertexIndex = 0,
            Weights =
            [
                new BoneWeight(rootBone.Id, 2.0),
                new BoneWeight(spineBone.Id, 6.0)
            ]
        };

        Assert.False(influence.IsNormalized());
        influence.NormalizeWeights();
        Assert.True(influence.IsNormalized());
        Assert.Equal(0.25, influence.Weights[0].Weight, 6);
        Assert.Equal(0.75, influence.Weights[1].Weight, 6);

        skeleton.VertexInfluences.Add(influence);
        Assert.True(skeleton.Validate().IsValid);

        // Introduce cycle: rootBone parented to headBone
        rootBone.ParentBoneId = headBone.Id;
        Assert.True(skeleton.ContainsCycle());
        var invalidResult = skeleton.Validate();
        Assert.False(invalidResult.IsValid);
        Assert.Contains(invalidResult.Issues, i => i.Code == UsmErrorCodes.InvalidSkeleton);
    }

    [Fact]
    public void AnimationData_TrackAndKeyframeValidation_ShouldCatchInvalidTracks()
    {
        Guid targetBoneId = Guid.NewGuid();
        var anim = new AnimationData("walk_cycle", duration: 1.5, frameRate: 30.0);

        var track = new AnimationTrack
        {
            TargetId = targetBoneId,
            Channel = AnimationChannel.Rotation,
            Interpolation = AnimationInterpolation.Linear,
            Keyframes =
            [
                AnimationKeyframe.ForRotation(0.0, QuaternionD.Identity),
                AnimationKeyframe.ForRotation(0.75, QuaternionD.FromEulerAnglesDegrees(0, 45, 0)),
                AnimationKeyframe.ForRotation(1.5, QuaternionD.Identity)
            ]
        };

        anim.Tracks.Add(track);
        Assert.True(anim.Validate().IsValid);

        // Negative duration should fail validation with USM014
        anim.Duration = -1.0;
        var invalidResult = anim.Validate();
        Assert.False(invalidResult.IsValid);
        Assert.Contains(invalidResult.Issues, i => i.Code == UsmErrorCodes.InvalidAnimation);
    }
}

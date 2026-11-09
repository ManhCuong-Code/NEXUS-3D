using NEXUS3D.Domain.Animations;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Skeletons;

namespace NEXUS3D.Domain.Validation;

/// <summary>
/// Validates <see cref="SkeletonData"/> bone hierarchies, parent references, cycles, and vertex bone weights.
/// </summary>
public sealed class SkeletonValidator : IValidator<SkeletonData>
{
    public ValidationResult Validate(SkeletonData? skeleton)
    {
        var result = new ValidationResult();
        if (skeleton is null)
        {
            result.AddError(UsmErrorCodes.InvalidSkeleton, "SkeletonData cannot be null.", "Skeleton");
            return result;
        }

        if (skeleton.Id == Guid.Empty)
        {
            result.AddError(UsmErrorCodes.InvalidSkeleton, "Skeleton ID cannot be Guid.Empty.", "Skeleton.Id");
        }

        if (string.IsNullOrWhiteSpace(skeleton.Name))
        {
            result.AddError(UsmErrorCodes.InvalidSkeleton, "Skeleton name cannot be empty.", "Skeleton.Name", skeleton.Id);
        }

        var boneIds = new HashSet<Guid>();
        foreach (var bone in skeleton.Bones)
        {
            if (bone.Id == Guid.Empty)
            {
                result.AddError(UsmErrorCodes.InvalidSkeleton, $"Bone '{bone.Name}' has Guid.Empty.", "Skeleton.Bones", skeleton.Id);
                continue;
            }

            if (!boneIds.Add(bone.Id))
            {
                result.AddError(UsmErrorCodes.DuplicateId, $"Duplicate Bone ID '{bone.Id}' in skeleton '{skeleton.Name}'.", "Skeleton.Bones", bone.Id);
            }

            if (string.IsNullOrWhiteSpace(bone.Name))
            {
                result.AddError(UsmErrorCodes.InvalidSkeleton, "Bone name cannot be empty.", $"Bone[{bone.Id}].Name", bone.Id);
            }

            if (!bone.LocalTransform.IsValid)
            {
                result.AddError(UsmErrorCodes.InvalidTransform, $"Bone '{bone.Name}' has an invalid LocalTransform.", $"Bone[{bone.Id}].LocalTransform", bone.Id);
            }
        }

        foreach (var bone in skeleton.Bones)
        {
            if (bone.ParentBoneId.HasValue && !boneIds.Contains(bone.ParentBoneId.Value))
            {
                result.AddError(
                    UsmErrorCodes.InvalidSkeleton,
                    $"Bone '{bone.Name}' references non-existent ParentBoneId '{bone.ParentBoneId.Value}'.",
                    $"Bone[{bone.Id}].ParentBoneId",
                    bone.Id);
            }
        }

        if (skeleton.RootBoneId.HasValue && !boneIds.Contains(skeleton.RootBoneId.Value))
        {
            result.AddError(
                UsmErrorCodes.InvalidSkeleton,
                $"Skeleton RootBoneId '{skeleton.RootBoneId.Value}' does not exist in Bones list.",
                "Skeleton.RootBoneId",
                skeleton.Id);
        }

        if (skeleton.ContainsCycle())
        {
            result.AddError(
                UsmErrorCodes.InvalidSkeleton,
                $"Skeleton '{skeleton.Name}' contains a bone hierarchy cycle.",
                "Skeleton.Bones",
                skeleton.Id);
        }

        for (int i = 0; i < skeleton.VertexInfluences.Count; i++)
        {
            BoneInfluence inf = skeleton.VertexInfluences[i];
            if (!inf.IsNormalized())
            {
                result.AddError(
                    UsmErrorCodes.InvalidSkeleton,
                    $"VertexInfluence[{i}] (Vertex {inf.VertexIndex}) has invalid bone weights or total weight ({inf.TotalWeight:0.####}) != 1.0.",
                    $"Skeleton.VertexInfluences[{i}]",
                    skeleton.Id);
            }

            foreach (var bw in inf.Weights)
            {
                if (!boneIds.Contains(bw.BoneId))
                {
                    result.AddError(
                        UsmErrorCodes.InvalidSkeleton,
                        $"VertexInfluence[{i}] references unknown BoneId '{bw.BoneId}'.",
                        $"Skeleton.VertexInfluences[{i}].Weights",
                        skeleton.Id);
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Validates <see cref="AnimationData"/> duration, frame rate, tracks, and keyframe ordering.
/// </summary>
public sealed class AnimationValidator : IValidator<AnimationData>
{
    public ValidationResult Validate(AnimationData? animation)
    {
        var result = new ValidationResult();
        if (animation is null)
        {
            result.AddError(UsmErrorCodes.InvalidAnimation, "AnimationData cannot be null.", "Animation");
            return result;
        }

        if (animation.Id == Guid.Empty)
        {
            result.AddError(UsmErrorCodes.InvalidAnimation, "Animation ID cannot be Guid.Empty.", "Animation.Id");
        }

        if (string.IsNullOrWhiteSpace(animation.Name))
        {
            result.AddError(UsmErrorCodes.InvalidAnimation, "Animation name cannot be empty.", "Animation.Name", animation.Id);
        }

        if (!double.IsFinite(animation.Duration) || animation.Duration <= 0.0)
        {
            result.AddError(
                UsmErrorCodes.InvalidAnimation,
                $"Animation '{animation.Name}' Duration ({animation.Duration}) must be greater than zero.",
                "Animation.Duration",
                animation.Id);
        }

        if (!double.IsFinite(animation.FrameRate) || animation.FrameRate <= 0.0)
        {
            result.AddError(
                UsmErrorCodes.InvalidAnimation,
                $"Animation '{animation.Name}' FrameRate ({animation.FrameRate}) must be greater than zero.",
                "Animation.FrameRate",
                animation.Id);
        }

        for (int tIdx = 0; tIdx < animation.Tracks.Count; tIdx++)
        {
            AnimationTrack track = animation.Tracks[tIdx];
            if (track.TargetId == Guid.Empty)
            {
                result.AddError(
                    UsmErrorCodes.InvalidAnimation,
                    $"Animation track[{tIdx}] has an empty TargetId.",
                    $"Animation.Tracks[{tIdx}].TargetId",
                    animation.Id);
            }

            if (track.Keyframes.Count == 0)
            {
                result.AddWarning(
                    UsmErrorCodes.InvalidAnimation,
                    $"Animation track[{tIdx}] contains no keyframes.",
                    $"Animation.Tracks[{tIdx}].Keyframes",
                    animation.Id);
                continue;
            }

            double prevTime = double.MinValue;
            for (int kIdx = 0; kIdx < track.Keyframes.Count; kIdx++)
            {
                AnimationKeyframe kf = track.Keyframes[kIdx];
                if (!double.IsFinite(kf.Time) || kf.Time < 0.0 || (animation.Duration > 0 && kf.Time > animation.Duration + MathUtilities.LooseEpsilon))
                {
                    result.AddError(
                        UsmErrorCodes.InvalidAnimation,
                        $"Keyframe[{kIdx}] time ({kf.Time}) is outside valid range [0, {animation.Duration}].",
                        $"Animation.Tracks[{tIdx}].Keyframes[{kIdx}].Time",
                        animation.Id);
                }

                if (kIdx > 0 && MathUtilities.ApproximatelyEqual(kf.Time, prevTime, 1e-9))
                {
                    result.AddError(
                        UsmErrorCodes.InvalidAnimation,
                        $"Track[{tIdx}] has duplicate keyframe timestamp at t={kf.Time}.",
                        $"Animation.Tracks[{tIdx}].Keyframes[{kIdx}].Time",
                        animation.Id);
                }
                else if (kIdx > 0 && kf.Time < prevTime)
                {
                    result.AddError(
                        UsmErrorCodes.InvalidAnimation,
                        $"Track[{tIdx}] keyframes are not sorted in ascending time order ({kf.Time} < {prevTime}).",
                        $"Animation.Tracks[{tIdx}].Keyframes[{kIdx}].Time",
                        animation.Id);
                }

                if (!kf.Value.IsFinite())
                {
                    result.AddError(
                        UsmErrorCodes.InvalidAnimation,
                        $"Keyframe[{kIdx}] value contains NaN or Infinity.",
                        $"Animation.Tracks[{tIdx}].Keyframes[{kIdx}].Value",
                        animation.Id);
                }

                prevTime = kf.Time;
            }
        }

        return result;
    }
}


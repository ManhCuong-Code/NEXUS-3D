using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Domain.Skeletons;

/// <summary>
/// Represents a single bone's weight influence on a vertex.
/// </summary>
public readonly record struct BoneWeight(Guid BoneId, double Weight)
{
    public bool IsValid =>
        BoneId != Guid.Empty &&
        double.IsFinite(Weight) &&
        Weight >= 0.0 &&
        Weight <= 1.0;
}

/// <summary>
/// Aggregates an arbitrary number of <see cref="BoneWeight"/> influences for a vertex (not limited to 4).
/// </summary>
public sealed class BoneInfluence : IDeepCloneable<BoneInfluence>
{
    public int VertexIndex { get; set; }
    public List<BoneWeight> Weights { get; set; } = [];

    public double TotalWeight => Weights.Sum(w => w.Weight);

    public bool IsNormalized(double tolerance = 1e-4) =>
        Weights.Count > 0 &&
        Weights.All(w => w.IsValid) &&
        Math.Abs(TotalWeight - 1.0) <= tolerance;

    public void NormalizeWeights()
    {
        double sum = TotalWeight;
        if (sum <= MathUtilities.Epsilon)
        {
            return;
        }

        for (int i = 0; i < Weights.Count; i++)
        {
            Weights[i] = new BoneWeight(Weights[i].BoneId, Weights[i].Weight / sum);
        }
    }

    public BoneInfluence DeepClone() => new()
    {
        VertexIndex = VertexIndex,
        Weights = Weights.ToList()
    };
}

/// <summary>
/// Named attachment or effect locator point attached to a <see cref="BoneData"/>.
/// </summary>
public sealed class BoneLocator : IDeepCloneable<BoneLocator>
{
    public string Name { get; set; } = string.Empty;
    public Vector3D Offset { get; set; } = Vector3D.Zero;
    public Vector3D? RotationDegrees { get; set; }
    public bool? IgnoreInheritedScale { get; set; }

    public BoneLocator()
    {
    }

    public BoneLocator(string name, Vector3D offset, Vector3D? rotationDegrees = null, bool? ignoreInheritedScale = null)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
        Offset = offset;
        RotationDegrees = rotationDegrees;
        IgnoreInheritedScale = ignoreInheritedScale;
    }

    public BoneLocator DeepClone() => new()
    {
        Name = Name,
        Offset = Offset,
        RotationDegrees = RotationDegrees,
        IgnoreInheritedScale = IgnoreInheritedScale
    };
}

/// <summary>
/// Represents an individual bone node in a <see cref="SkeletonData"/> hierarchy.
/// </summary>
public sealed class BoneData : Entity, ISceneComponent, IDeepCloneable<BoneData>
{
    public string Name { get; set; } = "Bone";
    public Guid? ParentBoneId { get; set; }
    public Vector3D Pivot { get; set; } = Vector3D.Zero;
    public Vector3D BindRotationDegrees { get; set; } = Vector3D.Zero;
    public string? BindingExpression { get; set; }
    public Transform3D LocalTransform { get; set; } = Transform3D.Identity();
    public Matrix4x4D? InverseBindMatrix { get; set; }
    public List<Guid> ChildBoneIds { get; set; } = [];
    public List<BoneLocator> Locators { get; set; } = [];

    public BoneData()
    {
    }

    public BoneData(string name, Guid? parentBoneId = null)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
        ParentBoneId = parentBoneId;
    }

    public BoneData DeepClone() => new(Name, ParentBoneId)
    {
        Id = Id,
        Pivot = Pivot,
        BindRotationDegrees = BindRotationDegrees,
        BindingExpression = BindingExpression,
        LocalTransform = LocalTransform.DeepClone(),
        InverseBindMatrix = InverseBindMatrix,
        ChildBoneIds = ChildBoneIds.ToList(),
        Locators = Locators.Select(l => l.DeepClone()).ToList()
    };
}

/// <summary>
/// Represents an articulated bone hierarchy (skeleton) in the Universal Scene Model.
/// </summary>
public sealed class SkeletonData : Entity, ISceneComponent, IDeepCloneable<SkeletonData>
{
    public string Name { get; set; } = "Skeleton";
    public List<BoneData> Bones { get; set; } = [];
    public Guid? RootBoneId { get; set; }
    public List<BoneInfluence> VertexInfluences { get; set; } = [];

    public SkeletonData()
    {
    }

    public SkeletonData(string name)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
    }

    public BoneData? FindBone(Guid boneId) =>
        Bones.FirstOrDefault(b => b.Id == boneId);

    public bool ContainsCycle()
    {
        var map = new Dictionary<Guid, BoneData>();
        foreach (var bone in Bones)
        {
            map[bone.Id] = bone;
        }

        foreach (var bone in Bones)
        {
            var visited = new HashSet<Guid> { bone.Id };
            Guid? currentParentId = bone.ParentBoneId;

            while (currentParentId.HasValue)
            {
                if (!visited.Add(currentParentId.Value))
                {
                    return true;
                }

                if (!map.TryGetValue(currentParentId.Value, out var parentBone))
                {
                    break;
                }

                currentParentId = parentBone.ParentBoneId;
            }
        }

        return false;
    }

    public ValidationResult Validate() => new SkeletonValidator().Validate(this);

    public SkeletonData DeepClone() => new(Name)
    {
        Id = Id,
        RootBoneId = RootBoneId,
        Bones = Bones.Select(b => b.DeepClone()).ToList(),
        VertexInfluences = VertexInfluences.Select(v => v.DeepClone()).ToList()
    };
}


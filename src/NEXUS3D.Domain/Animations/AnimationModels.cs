using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Domain.Animations;

public enum AnimationChannel
{
    Translation = 0,
    Rotation = 1,
    Scale = 2,
    MorphWeight = 3,
    Custom = 4
}

public enum AnimationInterpolation
{
    Step = 0,
    Linear = 1,
    CubicSpline = 2,
    Bezier = 3
}

public enum AnimationValueKind
{
    Vector3 = 0,
    Quaternion = 1,
    Scalar = 2,
    Vector4 = 3
}

/// <summary>
/// Represents a single keyframe in an <see cref="AnimationTrack"/> with type-safe channel accessors.
/// </summary>
public sealed class AnimationKeyframe : IDeepCloneable<AnimationKeyframe>
{
    public double Time { get; set; }
    public AnimationValueKind ValueKind { get; set; } = AnimationValueKind.Vector3;
    public Vector4D Value { get; set; }
    public Vector4D? InTangent { get; set; }
    public Vector4D? OutTangent { get; set; }

    public Vector3D AsVector3() => new(Value.X, Value.Y, Value.Z);
    public QuaternionD AsQuaternion() => new(Value.X, Value.Y, Value.Z, Value.W);
    public double AsScalar() => Value.X;

    public static AnimationKeyframe ForTranslation(double time, Vector3D translation) => new()
    {
        Time = time,
        ValueKind = AnimationValueKind.Vector3,
        Value = new Vector4D(translation.X, translation.Y, translation.Z, 0.0)
    };

    public static AnimationKeyframe ForRotation(double time, QuaternionD rotation) => new()
    {
        Time = time,
        ValueKind = AnimationValueKind.Quaternion,
        Value = new Vector4D(rotation.X, rotation.Y, rotation.Z, rotation.W)
    };

    public static AnimationKeyframe ForScale(double time, Vector3D scale) => new()
    {
        Time = time,
        ValueKind = AnimationValueKind.Vector3,
        Value = new Vector4D(scale.X, scale.Y, scale.Z, 0.0)
    };

    public static AnimationKeyframe ForScalar(double time, double scalar) => new()
    {
        Time = time,
        ValueKind = AnimationValueKind.Scalar,
        Value = new Vector4D(scalar, 0.0, 0.0, 0.0)
    };

    public AnimationKeyframe DeepClone() => new()
    {
        Time = Time,
        ValueKind = ValueKind,
        Value = Value,
        InTangent = InTangent,
        OutTangent = OutTangent
    };
}

/// <summary>
/// Sequence of keyframes animating a specific channel of a target node or bone.
/// </summary>
public sealed class AnimationTrack : IDeepCloneable<AnimationTrack>
{
    public Guid TargetId { get; set; }
    public AnimationChannel Channel { get; set; } = AnimationChannel.Translation;
    public List<AnimationKeyframe> Keyframes { get; set; } = [];
    public AnimationInterpolation Interpolation { get; set; } = AnimationInterpolation.Linear;

    public AnimationTrack DeepClone() => new()
    {
        TargetId = TargetId,
        Channel = Channel,
        Interpolation = Interpolation,
        Keyframes = Keyframes.Select(k => k.DeepClone()).ToList()
    };
}

/// <summary>
/// Discrete timed trigger event embedded within an <see cref="AnimationData"/> clip.
/// </summary>
public sealed class AnimationEvent : IDeepCloneable<AnimationEvent>
{
    public double Time { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Parameter { get; set; }

    public AnimationEvent DeepClone() => new()
    {
        Time = Time,
        Name = Name,
        Parameter = Parameter
    };
}

/// <summary>
/// Represents an animation clip containing tracks and events.
/// </summary>
public sealed class AnimationData : Entity, ISceneComponent, IDeepCloneable<AnimationData>
{
    public string Name { get; set; } = "Animation";
    public double Duration { get; set; } = 1.0;
    public double FrameRate { get; set; } = 24.0;
    public List<AnimationTrack> Tracks { get; set; } = [];
    public List<AnimationEvent> Events { get; set; } = [];

    public AnimationData()
    {
    }

    public AnimationData(string name, double duration = 1.0, double frameRate = 24.0)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
        Duration = duration;
        FrameRate = frameRate;
    }

    public ValidationResult Validate() => new AnimationValidator().Validate(this);

    public AnimationData DeepClone() => new(Name, Duration, FrameRate)
    {
        Id = Id,
        Tracks = Tracks.Select(t => t.DeepClone()).ToList(),
        Events = Events.Select(e => e.DeepClone()).ToList()
    };
}


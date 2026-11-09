using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;

namespace NEXUS3D.Domain.Mathematics;

/// <summary>
/// Represents a 3D spatial transformation composed of Position, Rotation (Quaternion), and Scale.
/// </summary>
public sealed class Transform3D : IDeepCloneable<Transform3D>
{
    private Vector3D _position = Vector3D.Zero;
    private QuaternionD _rotation = QuaternionD.Identity;
    private Vector3D _scale = Vector3D.One;

    public Vector3D Position
    {
        get => _position;
        set
        {
            if (!value.IsFinite())
            {
                throw new DomainException(UsmErrorCodes.InvalidTransform, "Transform Position must contain finite values.");
            }

            _position = value;
        }
    }

    public QuaternionD Rotation
    {
        get => _rotation;
        set
        {
            if (!value.IsFinite())
            {
                throw new DomainException(UsmErrorCodes.InvalidTransform, "Transform Rotation must contain finite values.");
            }

            _rotation = value.Normalize();
        }
    }

    public Vector3D Scale
    {
        get => _scale;
        set
        {
            if (!value.IsFinite())
            {
                throw new DomainException(UsmErrorCodes.InvalidTransform, "Transform Scale must contain finite values.");
            }

            _scale = value;
        }
    }

    /// <summary>
    /// Indicates whether the scale has an odd number of negative axes, which reverses polygon winding order and normals.
    /// </summary>
    public bool HasNegativeScale => (Scale.X * Scale.Y * Scale.Z) < 0.0;

    /// <summary>
    /// Indicates whether any scale axis is effectively zero (degenerate scale).
    /// </summary>
    public bool HasZeroScale =>
        MathUtilities.IsNearlyZero(Scale.X) ||
        MathUtilities.IsNearlyZero(Scale.Y) ||
        MathUtilities.IsNearlyZero(Scale.Z);

    public bool IsValid =>
        Position.IsFinite() &&
        Rotation.IsFinite() &&
        Scale.IsFinite() &&
        !HasZeroScale;

    public Transform3D()
    {
    }

    public Transform3D(Vector3D position, QuaternionD rotation, Vector3D scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    public static Transform3D Identity() => new();

    public Matrix4x4D ToMatrix() =>
        Matrix4x4D.CreateTRS(Position, Rotation, Scale);

    public Vector3D TransformPoint(Vector3D point) =>
        ToMatrix().TransformPoint(point);

    public Vector3D TransformDirection(Vector3D direction) =>
        ToMatrix().TransformDirection(direction);

    public Vector3D TransformNormal(Vector3D normal) =>
        ToMatrix().TransformNormal(normal);

    public Transform3D DeepClone() =>
        new(Position, Rotation, Scale);
}


using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Coordinates;

/// <summary>
/// Describes a 3D coordinate system convention (handedness, up axis, forward axis, and measurement unit).
/// </summary>
public sealed class CoordinateSystem
{
    public CoordinateHandedness Handedness { get; init; } = CoordinateHandedness.RightHanded;
    public AxisDirection UpAxis { get; init; } = AxisDirection.PositiveY;
    public AxisDirection ForwardAxis { get; init; } = AxisDirection.PositiveZ;
    public MeasurementUnit Unit { get; init; } = MeasurementUnit.Block;

    public Vector3D UpVector => AxisMapping.ToUnitVector(UpAxis);
    public Vector3D ForwardVector => AxisMapping.ToUnitVector(ForwardAxis);

    /// <summary>
    /// Computes the orthogonal Right vector consistent with <see cref="Handedness"/>, <see cref="UpAxis"/>, and <see cref="ForwardAxis"/>.
    /// </summary>
    public Vector3D RightVector => Handedness == CoordinateHandedness.RightHanded
        ? Vector3D.Cross(UpVector, ForwardVector)
        : Vector3D.Cross(ForwardVector, UpVector);

    public AxisDirection RightAxis => AxisMapping.FromUnitVector(RightVector);

    /// <summary>
    /// Validates that UpAxis and ForwardAxis are orthogonal and produce a non-zero RightVector.
    /// </summary>
    public bool IsValid
    {
        get
        {
            Vector3D u = UpVector;
            Vector3D f = ForwardVector;
            return MathUtilities.IsNearlyZero(Vector3D.Dot(u, f)) &&
                   RightVector.LengthSquared() > 0.5;
        }
    }

    public static CoordinateSystem DefaultUsm => new()
    {
        Handedness = CoordinateHandedness.RightHanded,
        UpAxis = AxisDirection.PositiveY,
        ForwardAxis = AxisDirection.PositiveZ,
        Unit = MeasurementUnit.Block
    };

    public static CoordinateSystem WavefrontObjDefault => new()
    {
        Handedness = CoordinateHandedness.RightHanded,
        UpAxis = AxisDirection.PositiveY,
        ForwardAxis = AxisDirection.PositiveZ,
        Unit = MeasurementUnit.Meter
    };

    public static CoordinateSystem ZUpRightHanded => new()
    {
        Handedness = CoordinateHandedness.RightHanded,
        UpAxis = AxisDirection.PositiveZ,
        ForwardAxis = AxisDirection.NegativeY,
        Unit = MeasurementUnit.Meter
    };

    public static CoordinateSystem BedrockDefault => new()
    {
        Handedness = CoordinateHandedness.LeftHanded,
        UpAxis = AxisDirection.PositiveY,
        ForwardAxis = AxisDirection.PositiveZ,
        Unit = MeasurementUnit.Block
    };

    public CoordinateSystem DeepClone() => new()
    {
        Handedness = Handedness,
        UpAxis = UpAxis,
        ForwardAxis = ForwardAxis,
        Unit = Unit
    };
}


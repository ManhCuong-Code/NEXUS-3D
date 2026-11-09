using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Coordinates;

/// <summary>
/// Defines an explicit mapping of Right, Up, and Forward semantic axes and provides basis vector utilities.
/// </summary>
public sealed record AxisMapping(
    AxisDirection RightAxis,
    AxisDirection UpAxis,
    AxisDirection ForwardAxis)
{
    public static AxisMapping DefaultRightHandedYUp => new(
        AxisDirection.PositiveX,
        AxisDirection.PositiveY,
        AxisDirection.PositiveZ);

    public Vector3D RightVector => ToUnitVector(RightAxis);
    public Vector3D UpVector => ToUnitVector(UpAxis);
    public Vector3D ForwardVector => ToUnitVector(ForwardAxis);

    public bool IsOrthogonal
    {
        get
        {
            Vector3D r = RightVector;
            Vector3D u = UpVector;
            Vector3D f = ForwardVector;

            return MathUtilities.IsNearlyZero(Vector3D.Dot(r, u)) &&
                   MathUtilities.IsNearlyZero(Vector3D.Dot(u, f)) &&
                   MathUtilities.IsNearlyZero(Vector3D.Dot(r, f));
        }
    }

    public static Vector3D ToUnitVector(AxisDirection direction) => direction switch
    {
        AxisDirection.PositiveX => Vector3D.UnitX,
        AxisDirection.NegativeX => -Vector3D.UnitX,
        AxisDirection.PositiveY => Vector3D.UnitY,
        AxisDirection.NegativeY => -Vector3D.UnitY,
        AxisDirection.PositiveZ => Vector3D.UnitZ,
        AxisDirection.NegativeZ => -Vector3D.UnitZ,
        _ => Vector3D.UnitY
    };

    public static AxisDirection FromUnitVector(Vector3D vector)
    {
        Vector3D n = vector.Normalize();
        if (MathUtilities.ApproximatelyEqual(n.X, 1.0)) return AxisDirection.PositiveX;
        if (MathUtilities.ApproximatelyEqual(n.X, -1.0)) return AxisDirection.NegativeX;
        if (MathUtilities.ApproximatelyEqual(n.Y, 1.0)) return AxisDirection.PositiveY;
        if (MathUtilities.ApproximatelyEqual(n.Y, -1.0)) return AxisDirection.NegativeY;
        if (MathUtilities.ApproximatelyEqual(n.Z, 1.0)) return AxisDirection.PositiveZ;
        return AxisDirection.NegativeZ;
    }

    public static double GetMetersPerUnit(MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Meter => 1.0,
        MeasurementUnit.Centimeter => 0.01,
        MeasurementUnit.Millimeter => 0.001,
        MeasurementUnit.Block => 1.0, // 1 Minecraft block = 1 meter canonical scale
        MeasurementUnit.Inch => 0.0254,
        MeasurementUnit.Foot => 0.3048,
        MeasurementUnit.Unitless => 1.0,
        _ => 1.0
    };

    public static double GetUnitScaleFactor(MeasurementUnit sourceUnit, MeasurementUnit targetUnit)
    {
        if (sourceUnit == targetUnit)
        {
            return 1.0;
        }

        double sourceMeters = GetMetersPerUnit(sourceUnit);
        double targetMeters = GetMetersPerUnit(targetUnit);
        return sourceMeters / targetMeters;
    }
}


namespace NEXUS3D.Domain.Coordinates;

/// <summary>
/// Handedness of a 3D Cartesian coordinate system.
/// </summary>
public enum CoordinateHandedness
{
    RightHanded = 0,
    LeftHanded = 1
}

/// <summary>
/// Six cardinal axis directions in 3D Cartesian space.
/// </summary>
public enum AxisDirection
{
    PositiveX = 0,
    NegativeX = 1,
    PositiveY = 2,
    NegativeY = 3,
    PositiveZ = 4,
    NegativeZ = 5
}

/// <summary>
/// Physical or logical measurement unit of a 3D scene.
/// </summary>
public enum MeasurementUnit
{
    Unitless = 0,
    Meter = 1,
    Centimeter = 2,
    Millimeter = 3,
    Block = 4,
    Inch = 5,
    Foot = 6
}


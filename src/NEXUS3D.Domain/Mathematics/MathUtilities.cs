namespace NEXUS3D.Domain.Mathematics;

/// <summary>
/// Precision constants and mathematical helper functions for the Universal Scene Model.
/// </summary>
public static class MathUtilities
{
    public const double Epsilon = 1e-12;
    public const double LooseEpsilon = 1e-6;
    public const double DegToRad = Math.PI / 180.0;
    public const double RadToDeg = 180.0 / Math.PI;

    public static double DegreesToRadians(double degrees) => degrees * DegToRad;

    public static double RadiansToDegrees(double radians) => radians * RadToDeg;

    public static bool IsNearlyZero(double value, double tolerance = Epsilon) =>
        Math.Abs(value) <= tolerance;

    public static bool ApproximatelyEqual(double a, double b, double tolerance = LooseEpsilon) =>
        Math.Abs(a - b) <= tolerance;

    public static double Lerp(double start, double end, double amount) =>
        start + (end - start) * Math.Clamp(amount, 0.0, 1.0);
}


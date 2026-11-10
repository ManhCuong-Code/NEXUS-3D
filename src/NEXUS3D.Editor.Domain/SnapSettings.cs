using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Editor.Domain;

/// <summary>
/// Configurable transformation snapping rules.
/// </summary>
public sealed class SnapSettings
{
    public bool Enabled { get; set; } = false;
    public double TranslationSnap { get; set; } = 0.5;
    public double RotationSnapDegrees { get; set; } = 15.0;
    public double ScaleSnap { get; set; } = 0.1;

    public static SnapSettings Default => new();

    public double SnapTranslation(double value)
    {
        if (!Enabled || TranslationSnap <= MathUtilities.Epsilon) return value;
        return Math.Round(value / TranslationSnap) * TranslationSnap;
    }

    public Vector3D SnapTranslation(Vector3D vector)
    {
        if (!Enabled || TranslationSnap <= MathUtilities.Epsilon) return vector;
        return new Vector3D(
            SnapTranslation(vector.X),
            SnapTranslation(vector.Y),
            SnapTranslation(vector.Z));
    }

    public double SnapRotation(double degrees)
    {
        if (!Enabled || RotationSnapDegrees <= MathUtilities.Epsilon) return degrees;
        return Math.Round(degrees / RotationSnapDegrees) * RotationSnapDegrees;
    }

    public double SnapScale(double scale)
    {
        if (!Enabled || ScaleSnap <= MathUtilities.Epsilon) return scale;
        double snapped = Math.Round(scale / ScaleSnap) * ScaleSnap;
        return Math.Abs(snapped) < 0.001 ? (scale < 0 ? -0.001 : 0.001) : snapped;
    }

    public Vector3D SnapScale(Vector3D scale)
    {
        if (!Enabled || ScaleSnap <= MathUtilities.Epsilon) return scale;
        return new Vector3D(
            SnapScale(scale.X),
            SnapScale(scale.Y),
            SnapScale(scale.Z));
    }
}


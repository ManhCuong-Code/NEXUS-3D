using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Coordinates;

/// <summary>
/// Tracks the source coordinate system, current active coordinate system, unit scale factors,
/// axis mapping, and origin offset of a <see cref="Scene.Scene3D"/>.
/// </summary>
public sealed class CoordinateMetadata : IDeepCloneable<CoordinateMetadata>
{
    public CoordinateSystem SourceSystem { get; set; } = CoordinateSystem.DefaultUsm;
    public CoordinateSystem CurrentSystem { get; set; } = CoordinateSystem.DefaultUsm;
    public MeasurementUnit OriginalUnit { get; set; } = MeasurementUnit.Block;
    public MeasurementUnit CurrentUnit { get; set; } = MeasurementUnit.Block;
    public Matrix4x4D AppliedConversionMatrix { get; set; } = Matrix4x4D.Identity;
    public double ScaleFactor { get; set; } = 1.0;
    public AxisMapping AxisMapping { get; set; } = AxisMapping.DefaultRightHandedYUp;
    public Vector3D Offset { get; set; } = Vector3D.Zero;
    public string SourceOriginDescription { get; set; } = "Default USM Origin";

    public bool RequiresWindingOrderFlip =>
        SourceSystem.Handedness != CurrentSystem.Handedness || AppliedConversionMatrix.HasNegativeScale;

    public static CoordinateMetadata CreateDefault() => new();

    public CoordinateMetadata DeepClone() => new()
    {
        SourceSystem = SourceSystem.DeepClone(),
        CurrentSystem = CurrentSystem.DeepClone(),
        OriginalUnit = OriginalUnit,
        CurrentUnit = CurrentUnit,
        AppliedConversionMatrix = AppliedConversionMatrix,
        ScaleFactor = ScaleFactor,
        AxisMapping = AxisMapping,
        Offset = Offset,
        SourceOriginDescription = SourceOriginDescription
    };
}

/// <summary>
/// Mathematical contract for converting positions, directions, and basis matrices between two <see cref="CoordinateSystem"/> conventions.
/// </summary>
public interface ICoordinateConverter
{
    Matrix4x4D CreateConversionMatrix(CoordinateSystem source, CoordinateSystem target);
    Vector3D ConvertPosition(Vector3D position, CoordinateSystem source, CoordinateSystem target);
    Vector3D ConvertDirection(Vector3D direction, CoordinateSystem source, CoordinateSystem target);
    bool RequiresWindingFlip(CoordinateSystem source, CoordinateSystem target);
}

/// <summary>
/// Pure mathematical implementation of <see cref="ICoordinateConverter"/>.
/// Handles axis permutation, sign inversion, handedness reflection, and measurement unit scaling.
/// </summary>
public sealed class CoordinateConverter : ICoordinateConverter
{
    public Matrix4x4D CreateConversionMatrix(CoordinateSystem source, CoordinateSystem target)
    {
        DomainGuard.NotNull(source);
        DomainGuard.NotNull(target);

        if (!source.IsValid || !target.IsValid)
        {
            throw new DomainException(
                UsmErrorCodes.InvalidCoordinateSystem,
                "Cannot create conversion matrix for non-orthogonal coordinate systems.");
        }

        Vector3D rs = source.RightVector;
        Vector3D us = source.UpVector;
        Vector3D fs = source.ForwardVector;

        Vector3D rt = target.RightVector;
        Vector3D ut = target.UpVector;
        Vector3D ft = target.ForwardVector;

        double unitScale = AxisMapping.GetUnitScaleFactor(source.Unit, target.Unit);

        // R = unitScale * B_target * (B_source)^T
        double m11 = (rt.X * rs.X + ut.X * us.X + ft.X * fs.X) * unitScale;
        double m12 = (rt.X * rs.Y + ut.X * us.Y + ft.X * fs.Y) * unitScale;
        double m13 = (rt.X * rs.Z + ut.X * us.Z + ft.X * fs.Z) * unitScale;

        double m21 = (rt.Y * rs.X + ut.Y * us.X + ft.Y * fs.X) * unitScale;
        double m22 = (rt.Y * rs.Y + ut.Y * us.Y + ft.Y * fs.Y) * unitScale;
        double m23 = (rt.Y * rs.Z + ut.Y * us.Z + ft.Y * fs.Z) * unitScale;

        double m31 = (rt.Z * rs.X + ut.Z * us.X + ft.Z * fs.X) * unitScale;
        double m32 = (rt.Z * rs.Y + ut.Z * us.Y + ft.Z * fs.Y) * unitScale;
        double m33 = (rt.Z * rs.Z + ut.Z * us.Z + ft.Z * fs.Z) * unitScale;

        return new Matrix4x4D(
            m11, m12, m13, 0.0,
            m21, m22, m23, 0.0,
            m31, m32, m33, 0.0,
            0.0, 0.0, 0.0, 1.0);
    }

    public Vector3D ConvertPosition(Vector3D position, CoordinateSystem source, CoordinateSystem target)
    {
        Matrix4x4D matrix = CreateConversionMatrix(source, target);
        return matrix.TransformPoint(position);
    }

    public Vector3D ConvertDirection(Vector3D direction, CoordinateSystem source, CoordinateSystem target)
    {
        DomainGuard.NotNull(source);
        DomainGuard.NotNull(target);

        // Direction conversion excludes unit scaling so unit directions remain unit length.
        var unitlessSource = new CoordinateSystem
        {
            Handedness = source.Handedness,
            UpAxis = source.UpAxis,
            ForwardAxis = source.ForwardAxis,
            Unit = MeasurementUnit.Unitless
        };

        var unitlessTarget = new CoordinateSystem
        {
            Handedness = target.Handedness,
            UpAxis = target.UpAxis,
            ForwardAxis = target.ForwardAxis,
            Unit = MeasurementUnit.Unitless
        };

        Matrix4x4D basisMatrix = CreateConversionMatrix(unitlessSource, unitlessTarget);
        return basisMatrix.TransformDirection(direction);
    }

    public bool RequiresWindingFlip(CoordinateSystem source, CoordinateSystem target)
    {
        DomainGuard.NotNull(source);
        DomainGuard.NotNull(target);
        return source.Handedness != target.Handedness;
    }
}


using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Editor.Engine.Mathematics;

/// <summary>
/// Geometric and matrix utility methods for transformation decompose, Euler conversions, and snapping.
/// </summary>
public static class TransformMath
{
    /// <summary>
    /// Decomposes a 4x4 matrix into Translation, Rotation (Quaternion), and Scale.
    /// </summary>
    public static (Vector3D Position, QuaternionD Rotation, Vector3D Scale) Decompose(Matrix4x4D m)
    {
        var position = new Vector3D(m.M14, m.M24, m.M34);

        double sx = new Vector3D(m.M11, m.M21, m.M31).Length();
        double sy = new Vector3D(m.M12, m.M22, m.M32).Length();
        double sz = new Vector3D(m.M13, m.M23, m.M33).Length();

        if (m.Determinant3x3() < 0)
        {
            sx = -sx;
        }

        if (Math.Abs(sx) <= MathUtilities.Epsilon) sx = 1.0;
        if (Math.Abs(sy) <= MathUtilities.Epsilon) sy = 1.0;
        if (Math.Abs(sz) <= MathUtilities.Epsilon) sz = 1.0;

        var scale = new Vector3D(sx, sy, sz);

        var rot = RotationMatrixToQuaternion(
            m.M11 / sx, m.M12 / sy, m.M13 / sz,
            m.M21 / sx, m.M22 / sy, m.M23 / sz,
            m.M31 / sx, m.M32 / sy, m.M33 / sz);

        return (position, rot, scale);
    }

    /// <summary>
    /// Converts a 3x3 orthonormal rotation matrix to a normalized QuaternionD.
    /// </summary>
    public static QuaternionD RotationMatrixToQuaternion(
        double m11, double m12, double m13,
        double m21, double m22, double m23,
        double m31, double m32, double m33)
    {
        double trace = m11 + m22 + m33;
        double x, y, z, w;

        if (trace > 0.0)
        {
            double s = Math.Sqrt(trace + 1.0) * 2.0;
            w = 0.25 * s;
            x = (m32 - m23) / s;
            y = (m13 - m31) / s;
            z = (m21 - m12) / s;
        }
        else if (m11 > m22 && m11 > m33)
        {
            double s = Math.Sqrt(1.0 + m11 - m22 - m33) * 2.0;
            w = (m32 - m23) / s;
            x = 0.25 * s;
            y = (m12 + m21) / s;
            z = (m13 + m31) / s;
        }
        else if (m22 > m33)
        {
            double s = Math.Sqrt(1.0 + m22 - m11 - m33) * 2.0;
            w = (m13 - m31) / s;
            x = (m12 + m21) / s;
            y = 0.25 * s;
            z = (m23 + m32) / s;
        }
        else
        {
            double s = Math.Sqrt(1.0 + m33 - m11 - m22) * 2.0;
            w = (m21 - m12) / s;
            x = (m13 + m31) / s;
            y = (m23 + m32) / s;
            z = 0.25 * s;
        }

        return new QuaternionD(x, y, z, w).Normalize();
    }

    /// <summary>
    /// Converts a Quaternion to Euler angles in degrees (Pitch X, Yaw Y, Roll Z).
    /// </summary>
    public static Vector3D QuaternionToEulerDegrees(QuaternionD q)
    {
        q = q.Normalize();

        // Roll (z-axis rotation)
        double sinr_cosp = 2.0 * (q.W * q.Z + q.X * q.Y);
        double cosr_cosp = 1.0 - 2.0 * (q.Y * q.Y + q.Z * q.Z);
        double roll = Math.Atan2(sinr_cosp, cosr_cosp);

        // Pitch (x-axis rotation)
        double sinp = 2.0 * (q.W * q.X - q.Y * q.Z);
        double pitch;
        if (Math.Abs(sinp) >= 1.0)
            pitch = Math.CopySign(Math.PI / 2.0, sinp);
        else
            pitch = Math.Asin(sinp);

        // Yaw (y-axis rotation)
        double siny_cosp = 2.0 * (q.W * q.Y + q.Z * q.X);
        double cosy_cosp = 1.0 - 2.0 * (q.X * q.X + q.Y * q.Y);
        double yaw = Math.Atan2(siny_cosp, cosy_cosp);

        return new Vector3D(
            MathUtilities.RadiansToDegrees(pitch),
            MathUtilities.RadiansToDegrees(yaw),
            MathUtilities.RadiansToDegrees(roll));
    }
}

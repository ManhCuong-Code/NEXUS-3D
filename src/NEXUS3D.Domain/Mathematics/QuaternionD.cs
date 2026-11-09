namespace NEXUS3D.Domain.Mathematics;

/// <summary>
/// Double-precision Hamilton quaternion (X, Y, Z, W) representing 3D rotations without gimbal lock.
/// </summary>
public readonly record struct QuaternionD(double X, double Y, double Z, double W)
{
    public static QuaternionD Identity => new(0.0, 0.0, 0.0, 1.0);

    public double LengthSquared() => X * X + Y * Y + Z * Z + W * W;

    public double Length() => Math.Sqrt(LengthSquared());

    public bool IsFinite() =>
        double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z) && double.IsFinite(W);

    public bool IsNormalized(double tolerance = MathUtilities.LooseEpsilon) =>
        IsFinite() && Math.Abs(LengthSquared() - 1.0) <= tolerance;

    public QuaternionD Normalize()
    {
        double length = Length();
        if (length <= MathUtilities.Epsilon)
        {
            return Identity;
        }

        return new QuaternionD(X / length, Y / length, Z / length, W / length);
    }

    public QuaternionD Conjugate() => new(-X, -Y, -Z, W);

    public QuaternionD Inverse()
    {
        double normSq = LengthSquared();
        if (normSq <= MathUtilities.Epsilon)
        {
            return Identity;
        }

        return new QuaternionD(-X / normSq, -Y / normSq, -Z / normSq, W / normSq);
    }

    public static QuaternionD Multiply(QuaternionD a, QuaternionD b) =>
        new(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

    public static QuaternionD operator *(QuaternionD a, QuaternionD b) => Multiply(a, b);

    public static QuaternionD FromAxisAngle(Vector3D axis, double radians)
    {
        Vector3D unitAxis = axis.Normalize();
        if (unitAxis.LengthSquared() <= MathUtilities.Epsilon)
        {
            return Identity;
        }

        double halfAngle = radians * 0.5;
        double sin = Math.Sin(halfAngle);
        double cos = Math.Cos(halfAngle);

        return new QuaternionD(
            unitAxis.X * sin,
            unitAxis.Y * sin,
            unitAxis.Z * sin,
            cos).Normalize();
    }

    public static QuaternionD FromEulerAnglesDegrees(double pitchX, double yawY, double rollZ)
    {
        var qx = FromAxisAngle(Vector3D.UnitX, MathUtilities.DegreesToRadians(pitchX));
        var qy = FromAxisAngle(Vector3D.UnitY, MathUtilities.DegreesToRadians(yawY));
        var qz = FromAxisAngle(Vector3D.UnitZ, MathUtilities.DegreesToRadians(rollZ));
        return (qy * qx * qz).Normalize();
    }

    public Vector3D RotateVector(Vector3D v)
    {
        QuaternionD q = Normalize();
        var u = new Vector3D(q.X, q.Y, q.Z);
        double s = q.W;

        return 2.0 * Vector3D.Dot(u, v) * u
             + (s * s - Vector3D.Dot(u, u)) * v
             + 2.0 * s * Vector3D.Cross(u, v);
    }

    public static QuaternionD Slerp(QuaternionD a, QuaternionD b, double amount)
    {
        double t = Math.Clamp(amount, 0.0, 1.0);
        QuaternionD q1 = a.Normalize();
        QuaternionD q2 = b.Normalize();

        double dot = q1.X * q2.X + q1.Y * q2.Y + q1.Z * q2.Z + q1.W * q2.W;
        if (dot < 0.0)
        {
            dot = -dot;
            q2 = new QuaternionD(-q2.X, -q2.Y, -q2.Z, -q2.W);
        }

        if (dot > 0.9995)
        {
            return new QuaternionD(
                MathUtilities.Lerp(q1.X, q2.X, t),
                MathUtilities.Lerp(q1.Y, q2.Y, t),
                MathUtilities.Lerp(q1.Z, q2.Z, t),
                MathUtilities.Lerp(q1.W, q2.W, t)).Normalize();
        }

        double theta0 = Math.Acos(Math.Clamp(dot, -1.0, 1.0));
        double theta = theta0 * t;
        double sinTheta = Math.Sin(theta);
        double sinTheta0 = Math.Sin(theta0);

        double s0 = Math.Cos(theta) - dot * sinTheta / sinTheta0;
        double s1 = sinTheta / sinTheta0;

        return new QuaternionD(
            s0 * q1.X + s1 * q2.X,
            s0 * q1.Y + s1 * q2.Y,
            s0 * q1.Z + s1 * q2.Z,
            s0 * q1.W + s1 * q2.W).Normalize();
    }
}


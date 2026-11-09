namespace NEXUS3D.Domain.Mathematics;

/// <summary>
/// Double-precision 4x4 transformation matrix.
/// <para>
/// <b>Mathematical Conventions (USM Standard):</b>
/// <list type="bullet">
///   <item>Storage layout: Row-major fields (<c>M11..M44</c> where first digit is row, second digit is column).</item>
///   <item>Vector convention: Column-vector multiplication (<c>v' = M * v</c>).</item>
///   <item>Translation components reside in the 4th column (<c>M14 = Tx, M24 = Ty, M34 = Tz</c>).</item>
///   <item>TRS composition order: <c>M = Translation * Rotation * Scale</c> (Scale is applied first, then Rotation, then Translation).</item>
///   <item>Hierarchy composition: <c>WorldMatrix = ParentWorldMatrix * LocalMatrix</c>.</item>
/// </list>
/// </para>
/// </summary>
public readonly record struct Matrix4x4D(
    double M11, double M12, double M13, double M14,
    double M21, double M22, double M23, double M24,
    double M31, double M32, double M33, double M34,
    double M41, double M42, double M43, double M44)
{
    public static Matrix4x4D Identity => new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);

    public bool IsFinite() =>
        double.IsFinite(M11) && double.IsFinite(M12) && double.IsFinite(M13) && double.IsFinite(M14) &&
        double.IsFinite(M21) && double.IsFinite(M22) && double.IsFinite(M23) && double.IsFinite(M24) &&
        double.IsFinite(M31) && double.IsFinite(M32) && double.IsFinite(M33) && double.IsFinite(M34) &&
        double.IsFinite(M41) && double.IsFinite(M42) && double.IsFinite(M43) && double.IsFinite(M44);

    public Vector3D Translation => new(M14, M24, M34);

    public double Determinant3x3() =>
        M11 * (M22 * M33 - M23 * M32) -
        M12 * (M21 * M33 - M23 * M31) +
        M13 * (M21 * M32 - M22 * M31);

    public double Determinant()
    {
        double a = M11, b = M12, c = M13, d = M14;
        double e = M21, f = M22, g = M23, h = M24;
        double i = M31, j = M32, k = M33, l = M34;
        double m = M41, n = M42, o = M43, p = M44;

        double kp_lo = k * p - l * o;
        double jp_ln = j * p - l * n;
        double jo_kn = j * o - k * n;
        double ip_lm = i * p - l * m;
        double io_km = i * o - k * m;
        double in_jm = i * n - j * m;

        return a * (f * kp_lo - g * jp_ln + h * jo_kn)
             - b * (e * kp_lo - g * ip_lm + h * io_km)
             + c * (e * jp_ln - f * ip_lm + h * in_jm)
             - d * (e * jo_kn - f * io_km + g * in_jm);
    }

    public bool HasNegativeScale => Determinant3x3() < 0.0;

    public static Matrix4x4D CreateTranslation(Vector3D position) =>
        new(
            1, 0, 0, position.X,
            0, 1, 0, position.Y,
            0, 0, 1, position.Z,
            0, 0, 0, 1);

    public static Matrix4x4D CreateScale(Vector3D scale) =>
        new(
            scale.X, 0, 0, 0,
            0, scale.Y, 0, 0,
            0, 0, scale.Z, 0,
            0, 0, 0, 1);

    public static Matrix4x4D CreateRotation(QuaternionD rotation)
    {
        QuaternionD q = rotation.Normalize();
        double xx = q.X * q.X;
        double yy = q.Y * q.Y;
        double zz = q.Z * q.Z;
        double xy = q.X * q.Y;
        double xz = q.X * q.Z;
        double yz = q.Y * q.Z;
        double wx = q.W * q.X;
        double wy = q.W * q.Y;
        double wz = q.W * q.Z;

        return new Matrix4x4D(
            1.0 - 2.0 * (yy + zz), 2.0 * (xy - wz),       2.0 * (xz + wy),       0.0,
            2.0 * (xy + wz),       1.0 - 2.0 * (xx + zz), 2.0 * (yz - wx),       0.0,
            2.0 * (xz - wy),       2.0 * (yz + wx),       1.0 - 2.0 * (xx + yy), 0.0,
            0.0,                   0.0,                   0.0,                   1.0);
    }

    public static Matrix4x4D CreateTRS(Vector3D position, QuaternionD rotation, Vector3D scale)
    {
        Matrix4x4D r = CreateRotation(rotation);
        return new Matrix4x4D(
            r.M11 * scale.X, r.M12 * scale.Y, r.M13 * scale.Z, position.X,
            r.M21 * scale.X, r.M22 * scale.Y, r.M23 * scale.Z, position.Y,
            r.M31 * scale.X, r.M32 * scale.Y, r.M33 * scale.Z, position.Z,
            0.0,             0.0,             0.0,             1.0);
    }

    public static Matrix4x4D Multiply(Matrix4x4D a, Matrix4x4D b) =>
        new(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
            a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,

            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
            a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,

            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
            a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,

            a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
            a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
            a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
            a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44);

    public static Matrix4x4D operator *(Matrix4x4D a, Matrix4x4D b) => Multiply(a, b);

    /// <summary>
    /// Transforms a 3D point (homogeneous W = 1, affected by translation, rotation, and scale).
    /// </summary>
    public Vector3D TransformPoint(Vector3D point)
    {
        double x = M11 * point.X + M12 * point.Y + M13 * point.Z + M14;
        double y = M21 * point.X + M22 * point.Y + M23 * point.Z + M24;
        double z = M31 * point.X + M32 * point.Y + M33 * point.Z + M34;
        double w = M41 * point.X + M42 * point.Y + M43 * point.Z + M44;

        if (!MathUtilities.ApproximatelyEqual(w, 1.0) && !MathUtilities.IsNearlyZero(w))
        {
            return new Vector3D(x / w, y / w, z / w);
        }

        return new Vector3D(x, y, z);
    }

    /// <summary>
    /// Transforms a 3D direction vector (homogeneous W = 0, unaffected by translation).
    /// </summary>
    public Vector3D TransformDirection(Vector3D direction) =>
        new(
            M11 * direction.X + M12 * direction.Y + M13 * direction.Z,
            M21 * direction.X + M22 * direction.Y + M23 * direction.Z,
            M31 * direction.X + M32 * direction.Y + M33 * direction.Z);

    /// <summary>
    /// Transforms a surface normal using the inverse-transpose of the upper-left 3x3 matrix
    /// to preserve perpendicularity under non-uniform scaling.
    /// </summary>
    public Vector3D TransformNormal(Vector3D normal)
    {
        if (TryInvert(out Matrix4x4D inv))
        {
            Matrix4x4D invTranspose = inv.Transpose();
            return invTranspose.TransformDirection(normal).Normalize();
        }

        return TransformDirection(normal).Normalize();
    }

    public Matrix4x4D Transpose() =>
        new(
            M11, M21, M31, M41,
            M12, M22, M32, M42,
            M13, M23, M33, M43,
            M14, M24, M34, M44);

    public bool TryInvert(out Matrix4x4D result)
    {
        double a = M11, b = M12, c = M13, d = M14;
        double e = M21, f = M22, g = M23, h = M24;
        double i = M31, j = M32, k = M33, l = M34;
        double m = M41, n = M42, o = M43, p = M44;

        double kp_lo = k * p - l * o;
        double jp_ln = j * p - l * n;
        double jo_kn = j * o - k * n;
        double ip_lm = i * p - l * m;
        double io_km = i * o - k * m;
        double in_jm = i * n - j * m;

        double a11 = +(f * kp_lo - g * jp_ln + h * jo_kn);
        double a12 = -(e * kp_lo - g * ip_lm + h * io_km);
        double a13 = +(e * jp_ln - f * ip_lm + h * in_jm);
        double a14 = -(e * jo_kn - f * io_km + g * in_jm);

        double det = a * a11 + b * a12 + c * a13 + d * a14;
        if (Math.Abs(det) <= MathUtilities.Epsilon || !double.IsFinite(det))
        {
            result = Identity;
            return false;
        }

        double invDet = 1.0 / det;

        double gp_ho = g * p - h * o;
        double fp_hn = f * p - h * n;
        double fo_gn = f * o - g * n;
        double ep_hm = e * p - h * m;
        double eo_gm = e * o - g * m;
        double en_fm = e * n - f * m;

        double gl_hk = g * l - h * k;
        double fl_hj = f * l - h * j;
        double fk_gj = f * k - g * j;
        double el_hi = e * l - h * i;
        double ek_gi = e * k - g * i;
        double ej_fi = e * j - f * i;

        result = new Matrix4x4D(
            a11 * invDet,
            -(b * kp_lo - c * jp_ln + d * jo_kn) * invDet,
            +(b * gp_ho - c * fp_hn + d * fo_gn) * invDet,
            -(b * gl_hk - c * fl_hj + d * fk_gj) * invDet,

            a12 * invDet,
            +(a * kp_lo - c * ip_lm + d * io_km) * invDet,
            -(a * gp_ho - c * ep_hm + d * eo_gm) * invDet,
            +(a * gl_hk - c * el_hi + d * ek_gi) * invDet,

            a13 * invDet,
            -(a * jp_ln - b * ip_lm + d * in_jm) * invDet,
            +(a * fp_hn - b * ep_hm + d * en_fm) * invDet,
            -(a * fl_hj - b * el_hi + d * ej_fi) * invDet,

            a14 * invDet,
            +(a * jo_kn - b * io_km + c * in_jm) * invDet,
            -(a * fo_gn - b * eo_gm + c * en_fm) * invDet,
            +(a * fk_gj - b * ek_gi + c * ej_fi) * invDet);

        return true;
    }
}


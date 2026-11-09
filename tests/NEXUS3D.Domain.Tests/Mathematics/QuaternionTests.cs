using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Tests.Mathematics;

public sealed class QuaternionTests
{
    [Fact]
    public void QuaternionD_Identity_ShouldBeUnitAndLeaveVectorUnchanged()
    {
        QuaternionD q = QuaternionD.Identity;
        Assert.True(q.IsFinite());
        Assert.True(q.IsNormalized());

        var v = new Vector3D(3.5, -2.0, 7.25);
        Vector3D rotated = q.RotateVector(v);
        Assert.Equal(v.X, rotated.X, 6);
        Assert.Equal(v.Y, rotated.Y, 6);
        Assert.Equal(v.Z, rotated.Z, 6);
    }

    [Fact]
    public void QuaternionD_AxisAngle_90DegreesAroundY_ShouldRotateXToNegativeZ()
    {
        QuaternionD q = QuaternionD.FromAxisAngle(Vector3D.UnitY, MathUtilities.DegreesToRadians(90.0));
        Vector3D rotated = q.RotateVector(Vector3D.UnitX);

        Assert.Equal(0.0, rotated.X, 6);
        Assert.Equal(0.0, rotated.Y, 6);
        Assert.Equal(-1.0, rotated.Z, 6);
    }

    [Fact]
    public void QuaternionD_EulerDegrees_InverseAndSlerp_ShouldComputeAccurately()
    {
        QuaternionD q = QuaternionD.FromEulerAnglesDegrees(0.0, 0.0, 90.0);
        Assert.True(q.IsNormalized());

        QuaternionD inv = q.Inverse();
        QuaternionD product = q * inv;
        Assert.Equal(0.0, product.X, 6);
        Assert.Equal(0.0, product.Y, 6);
        Assert.Equal(0.0, product.Z, 6);
        Assert.Equal(1.0, Math.Abs(product.W), 6);

        QuaternionD halfWay = QuaternionD.Slerp(QuaternionD.Identity, q, 0.5);
        QuaternionD expectedHalf = QuaternionD.FromAxisAngle(Vector3D.UnitZ, MathUtilities.DegreesToRadians(45.0));
        Assert.Equal(expectedHalf.X, halfWay.X, 6);
        Assert.Equal(expectedHalf.Y, halfWay.Y, 6);
        Assert.Equal(expectedHalf.Z, halfWay.Z, 6);
        Assert.Equal(expectedHalf.W, halfWay.W, 6);
    }
}

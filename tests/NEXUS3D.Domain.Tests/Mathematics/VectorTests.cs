using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Tests.Mathematics;

public sealed class VectorTests
{
    [Fact]
    public void Vector2D_BasicOperations_ShouldComputeAccurately()
    {
        var a = new Vector2D(3.0, 4.0);
        var b = new Vector2D(1.0, -2.0);

        Assert.Equal(5.0, a.Length(), 6);
        Assert.Equal(25.0, a.LengthSquared(), 6);
        Assert.Equal(new Vector2D(4.0, 2.0), a + b);
        Assert.Equal(new Vector2D(2.0, 6.0), a - b);
        Assert.Equal(-5.0, Vector2D.Dot(a, b), 6);

        Vector2D normalized = a.Normalize();
        Assert.Equal(0.6, normalized.X, 6);
        Assert.Equal(0.8, normalized.Y, 6);
        Assert.Equal(Vector2D.Zero, Vector2D.Zero.Normalize());
    }

    [Fact]
    public void Vector3D_DotAndCrossProduct_ShouldFollowRightHandRule()
    {
        Vector3D x = Vector3D.UnitX;
        Vector3D y = Vector3D.UnitY;
        Vector3D z = Vector3D.UnitZ;

        Assert.Equal(0.0, Vector3D.Dot(x, y), 9);
        Assert.Equal(z, Vector3D.Cross(x, y));
        Assert.Equal(x, Vector3D.Cross(y, z));
        Assert.Equal(y, Vector3D.Cross(z, x));
        Assert.Equal(-z, Vector3D.Cross(y, x));
    }

    [Fact]
    public void Vector3D_NormalizationAndValidity_ShouldHandleEdgeCasesSafely()
    {
        var v = new Vector3D(2.0, 3.0, 6.0);
        Assert.Equal(7.0, v.Length(), 9);
        Assert.True(v.IsFinite());

        Vector3D unit = v.Normalize();
        Assert.Equal(1.0, unit.Length(), 9);

        // Zero vector normalize must return Zero without NaN
        Vector3D zeroNorm = Vector3D.Zero.Normalize();
        Assert.Equal(Vector3D.Zero, zeroNorm);
        Assert.True(zeroNorm.IsFinite());

        var nanVec = new Vector3D(double.NaN, 1.0, 2.0);
        var infVec = new Vector3D(1.0, double.PositiveInfinity, 2.0);
        Assert.False(nanVec.IsFinite());
        Assert.False(infVec.IsFinite());
    }

    [Fact]
    public void Vector4D_Operations_ShouldComputeAccurately()
    {
        var v = new Vector4D(1.0, 2.0, 2.0, 4.0);
        Assert.Equal(5.0, v.Length(), 9);
        Assert.Equal(1.0, v.Normalize().Length(), 9);
    }
}

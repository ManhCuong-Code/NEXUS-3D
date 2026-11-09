using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Tests.Mathematics;

public sealed class Matrix4x4Tests
{
    [Fact]
    public void Matrix4x4D_TRSComposition_ShouldFollowColumnVectorOrder_T_Times_R_Times_S()
    {
        var translation = new Vector3D(10.0, 20.0, 30.0);
        QuaternionD rotation = QuaternionD.FromAxisAngle(Vector3D.UnitZ, MathUtilities.DegreesToRadians(90.0));
        var scale = new Vector3D(2.0, 3.0, 4.0);

        Matrix4x4D m = Matrix4x4D.CreateTRS(translation, rotation, scale);

        Vector3D transformed = m.TransformPoint(new Vector3D(1.0, 0.0, 0.0));
        Assert.Equal(10.0, transformed.X, 6);
        Assert.Equal(22.0, transformed.Y, 6);
        Assert.Equal(30.0, transformed.Z, 6);

        Vector3D transformedDir = m.TransformDirection(new Vector3D(1.0, 0.0, 0.0));
        Assert.Equal(0.0, transformedDir.X, 6);
        Assert.Equal(2.0, transformedDir.Y, 6);
        Assert.Equal(0.0, transformedDir.Z, 6);
    }

    [Fact]
    public void Matrix4x4D_TryInvert_ShouldRecoverOriginalPoint_AndFailForSingularMatrix()
    {
        Matrix4x4D m = Matrix4x4D.CreateTRS(
            new Vector3D(-5.0, 12.0, 8.0),
            QuaternionD.FromEulerAnglesDegrees(25.0, -40.0, 15.0),
            new Vector3D(1.5, 2.0, 0.5));

        Assert.True(m.TryInvert(out Matrix4x4D inv));

        var p = new Vector3D(7.0, -3.0, 11.0);
        Vector3D world = m.TransformPoint(p);
        Vector3D roundTrip = inv.TransformPoint(world);
        Assert.Equal(p.X, roundTrip.X, 6);
        Assert.Equal(p.Y, roundTrip.Y, 6);
        Assert.Equal(p.Z, roundTrip.Z, 6);

        Matrix4x4D singular = Matrix4x4D.CreateScale(new Vector3D(1.0, 0.0, 1.0));
        Assert.False(singular.TryInvert(out _));
    }
}

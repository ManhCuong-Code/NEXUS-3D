using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Tests.Mathematics;

public sealed class TransformAndBoundsTests
{
    [Fact]
    public void Transform3D_ValidationAndNegativeScale_ShouldDetectCorrectly()
    {
        Transform3D identity = Transform3D.Identity();
        Assert.True(identity.IsValid);
        Assert.False(identity.HasNegativeScale);

        var negativeScale = new Transform3D(Vector3D.Zero, QuaternionD.Identity, new Vector3D(-1.0, 1.0, 1.0));
        Assert.True(negativeScale.IsValid);
        Assert.True(negativeScale.HasNegativeScale);

        var zeroScale = new Transform3D(Vector3D.Zero, QuaternionD.Identity, new Vector3D(1.0, 0.0, 1.0));
        Assert.False(zeroScale.IsValid);
    }

    [Fact]
    public void BoundingBox_EncapsulateIntersectAndTransform_ShouldComputeAccurately()
    {
        BoundingBox box = BoundingBox.Empty;
        Assert.False(box.IsValid);

        box = box.Encapsulate(new Vector3D(-2.0, -3.0, -4.0));
        box = box.Encapsulate(new Vector3D(2.0, 3.0, 4.0));

        Assert.True(box.IsValid);
        Assert.Equal(Vector3D.Zero, box.Center);
        Assert.Equal(new Vector3D(4.0, 6.0, 8.0), box.Size);
        Assert.Equal(192.0, box.Volume, 6);
        Assert.True(box.Contains(new Vector3D(1.0, 1.0, 1.0)));
        Assert.False(box.Contains(new Vector3D(5.0, 0.0, 0.0)));

        var ray = new Ray3D(new Vector3D(0.0, 0.0, -10.0), Vector3D.UnitZ);
        Assert.True(ray.Intersects(box, out double hitDist));
        Assert.Equal(6.0, hitDist, 6);

        Matrix4x4D translate = Matrix4x4D.CreateTranslation(new Vector3D(10.0, 0.0, 0.0));
        BoundingBox transformed = box.Transform(translate);
        Assert.Equal(10.0, transformed.Center.X, 6);
        Assert.Equal(0.0, transformed.Center.Y, 6);
        Assert.Equal(0.0, transformed.Center.Z, 6);
    }

    [Fact]
    public void Plane3D_SignedDistance_ShouldComputeAccurately()
    {
        Plane3D plane = Plane3D.FromPointAndNormal(new Vector3D(0.0, 5.0, 0.0), Vector3D.UnitY);
        Assert.Equal(3.0, plane.SignedDistanceTo(new Vector3D(0.0, 8.0, 0.0)), 6);
    }
}

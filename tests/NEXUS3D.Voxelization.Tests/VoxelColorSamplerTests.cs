using System.Numerics;
using NEXUS3D.Voxelization.Engine.Processing;
using Xunit;

namespace NEXUS3D.Voxelization.Tests;

public class VoxelColorSamplerTests
{
    [Fact]
    public void ComputeBarycentric_PointAtVertexA_ReturnsUnitWeightForU()
    {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(10f, 0f, 0f);
        var c = new Vector3(0f, 10f, 0f);

        var bary = VoxelColorSampler.ComputeBarycentric(a, b, c, a);

        Assert.True(MathF.Abs(bary.X - 1.0f) < 1e-4f, $"Expected u ~ 1.0, got {bary.X}");
        Assert.True(MathF.Abs(bary.Y - 0.0f) < 1e-4f, $"Expected v ~ 0.0, got {bary.Y}");
        Assert.True(MathF.Abs(bary.Z - 0.0f) < 1e-4f, $"Expected w ~ 0.0, got {bary.Z}");
    }

    [Fact]
    public void ComputeBarycentric_PointAtTriangleCentroid_ReturnsEqualWeights()
    {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(3f, 0f, 0f);
        var c = new Vector3(0f, 3f, 0f);
        var centroid = (a + b + c) / 3f;

        var bary = VoxelColorSampler.ComputeBarycentric(a, b, c, centroid);

        Assert.True(MathF.Abs(bary.X - 1f / 3f) < 1e-4f);
        Assert.True(MathF.Abs(bary.Y - 1f / 3f) < 1e-4f);
        Assert.True(MathF.Abs(bary.Z - 1f / 3f) < 1e-4f);
    }
}

using System.Numerics;
using NEXUS3D.Voxelization.Engine.Geometry;
using Xunit;

namespace NEXUS3D.Voxelization.Tests;

public class TriangleBoxIntersectionTests
{
    [Fact]
    public void Intersects_TrianglePassingThroughCenter_ReturnsTrue()
    {
        // Box centered at origin, half-size (1, 1, 1)
        var boxCenter = Vector3.Zero;
        var boxHalfSize = new Vector3(1.0f, 1.0f, 1.0f);

        var v0 = new Vector3(-2.0f, 0.0f, 0.0f);
        var v1 = new Vector3(2.0f, 0.0f, 0.0f);
        var v2 = new Vector3(0.0f, 2.0f, 0.0f);

        bool result = TriangleBoxIntersection.Intersects(v0, v1, v2, boxCenter, boxHalfSize);

        Assert.True(result);
    }

    [Fact]
    public void Intersects_TriangleCompletelyOutsideBox_ReturnsFalse()
    {
        var boxCenter = Vector3.Zero;
        var boxHalfSize = new Vector3(1.0f, 1.0f, 1.0f);

        // Triangle far away along +X
        var v0 = new Vector3(10.0f, 0.0f, 0.0f);
        var v1 = new Vector3(12.0f, 0.0f, 0.0f);
        var v2 = new Vector3(11.0f, 2.0f, 0.0f);

        bool result = TriangleBoxIntersection.Intersects(v0, v1, v2, boxCenter, boxHalfSize);

        Assert.False(result);
    }

    [Fact]
    public void Intersects_TriangleInsideBox_ReturnsTrue()
    {
        var boxCenter = new Vector3(5.0f, 5.0f, 5.0f);
        var boxHalfSize = new Vector3(2.0f, 2.0f, 2.0f);

        var v0 = new Vector3(5.0f, 5.0f, 5.0f);
        var v1 = new Vector3(5.5f, 5.0f, 5.0f);
        var v2 = new Vector3(5.0f, 5.5f, 5.0f);

        bool result = TriangleBoxIntersection.Intersects(v0, v1, v2, boxCenter, boxHalfSize);

        Assert.True(result);
    }

    [Fact]
    public void Intersects_TriangleTouchingCornerOnly_ReturnsTrue()
    {
        var boxCenter = Vector3.Zero;
        var boxHalfSize = new Vector3(1.0f, 1.0f, 1.0f);

        // Corner at (1, 1, 1)
        var v0 = new Vector3(1.0f, 1.0f, 1.0f);
        var v1 = new Vector3(2.0f, 1.0f, 1.0f);
        var v2 = new Vector3(1.0f, 2.0f, 1.0f);

        bool result = TriangleBoxIntersection.Intersects(v0, v1, v2, boxCenter, boxHalfSize);

        Assert.True(result);
    }

    [Fact]
    public void Intersects_SeparatedAlongDiagonalEdgeCrossProduct_ReturnsFalse()
    {
        var boxCenter = Vector3.Zero;
        var boxHalfSize = new Vector3(1.0f, 1.0f, 1.0f);

        // Triangle near corner but separated by edge-cross axis
        var v0 = new Vector3(1.2f, 1.2f, 0.0f);
        var v1 = new Vector3(1.5f, 0.0f, 1.2f);
        var v2 = new Vector3(1.8f, 1.8f, 1.8f);

        bool result = TriangleBoxIntersection.Intersects(v0, v1, v2, boxCenter, boxHalfSize);

        Assert.False(result);
    }

    [Fact]
    public void Intersects_ConservativeExpansion_CapturesNearMiss()
    {
        var boxCenter = Vector3.Zero;
        var boxHalfSize = new Vector3(1.0f, 1.0f, 1.0f);

        // Triangle just outside (1.05 along X)
        var v0 = new Vector3(1.05f, -0.5f, -0.5f);
        var v1 = new Vector3(1.05f, 0.5f, -0.5f);
        var v2 = new Vector3(1.05f, 0.0f, 0.5f);

        bool exactResult = TriangleBoxIntersection.Intersects(v0, v1, v2, boxCenter, boxHalfSize, expansion: 0.0f);
        bool conservativeResult = TriangleBoxIntersection.Intersects(v0, v1, v2, boxCenter, boxHalfSize, expansion: 0.1f);

        Assert.False(exactResult);
        Assert.True(conservativeResult);
    }
}

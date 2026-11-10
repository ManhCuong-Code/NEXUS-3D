using System.Numerics;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class NormalVisualizerTests
{
    private readonly NormalVisualizer _visualizer = new();

    [Fact]
    public void GenerateNormalLines_CreatesLinePairsForNormals()
    {
        var mesh = new RenderMesh
        {
            Name = "Triangle",
            Positions = [new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)],
            Normals = [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ],
            Indices = [0, 1, 2]
        };

        var normalLines = _visualizer.GenerateNormalLines(mesh, normalLength: 0.5f);

        Assert.NotNull(normalLines);
        // 3 vertices -> 3 lines -> 6 positions
        Assert.Equal(6, normalLines.Positions.Length);
        Assert.Equal(6, normalLines.WireframeIndices.Length);
        Assert.Equal(new Vector3(0, 0, 0), normalLines.Positions[0]);
        Assert.Equal(new Vector3(0, 0, 0.5f), normalLines.Positions[1]);
    }

    [Fact]
    public void GenerateNormalLines_HandlesMeshWithoutNormalsGracefully()
    {
        var mesh = new RenderMesh
        {
            Positions = [new Vector3(0, 0, 0)],
            Normals = []
        };

        var normalLines = _visualizer.GenerateNormalLines(mesh);

        Assert.Empty(normalLines.Positions);
    }
}

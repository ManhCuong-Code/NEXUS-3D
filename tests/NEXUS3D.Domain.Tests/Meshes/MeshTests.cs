using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Factories;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Domain.Tests.Meshes;

public sealed class MeshTests
{
    [Fact]
    public void MeshFactory_CreateBox_ShouldProduceValidQuadMeshWithAccurateStatistics()
    {
        var factory = new MeshFactory();
        MeshData box = factory.CreateBox("UnitBox", new Vector3D(2.0, 4.0, 6.0));

        ValidationResult validation = box.Validate();
        Assert.True(validation.IsValid);

        MeshStatistics stats = box.CalculateStatistics();
        Assert.Equal(8, stats.VertexCount);
        Assert.Equal(6, stats.FaceCount);
        Assert.Equal(12, stats.EstimatedTriangleCount);
        Assert.True(stats.HasNormals);
        Assert.True(stats.HasTexCoords);
        Assert.Equal(new Vector3D(2.0, 4.0, 6.0), stats.Bounds.Size);
    }

    [Fact]
    public void MeshValidator_ShouldCatchInvalidVertexIndex_DegenerateFaces_AndNaNPositions()
    {
        var mesh = new MeshData("BrokenMesh");
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 0, 0), normal: new Vector3D(0, 5, 0))); // non-unit normal -> warning
        mesh.Vertices.Add(new Vertex(new Vector3D(1, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 1, 0)));

        // Out-of-bounds vertex index 99
        mesh.Faces.Add(new Face([0, 1, 99]));
        // Face with fewer than 3 indices
        mesh.Faces.Add(new Face([0, 1]));
        // Degenerate face with duplicate vertex index
        mesh.Faces.Add(new Face([0, 1, 1]));

        ValidationResult result = mesh.Validate();
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == UsmErrorCodes.InvalidVertexIndex && i.Severity == ValidationSeverity.Error);
        Assert.Contains(result.Issues, i => i.Code == UsmErrorCodes.InvalidMesh && i.Severity == ValidationSeverity.Error);
        Assert.Contains(result.Issues, i => i.Code == UsmErrorCodes.InvalidMesh && i.Severity == ValidationSeverity.Warning);
    }
}

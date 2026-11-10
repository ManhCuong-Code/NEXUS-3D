using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Engine.Services;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using Xunit;

namespace NEXUS3D.Conversion.Tests;

public sealed class MeshToVoxelConverterTests
{
    private readonly MeshToVoxelConverter _converter = new(
        new PolygonTriangulator(),
        NullLogger<MeshToVoxelConverter>.Instance);

    [Fact]
    public void ConvertMeshToVoxelGrid_CubeMesh_RasterizesSurfaceVoxels()
    {
        var mesh = new MeshData("Cube", MeshTopology.Triangles);
        // Add single unit cube quad
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(1, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(1, 1, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 1, 0)));
        mesh.Faces.Add(new Face([0, 1, 2]));
        mesh.Faces.Add(new Face([0, 2, 3]));

        var result = _converter.ConvertMeshToVoxelGrid(mesh, null, new MeshToVoxelOptions(VoxelSize: 1.0));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.GetVoxelCount() > 0);
    }

    [Fact]
    public void ConvertMeshToVoxelGrid_HollowCube_WithSolidFill_FillsInterior()
    {
        var mesh = new MeshData("BoundingBox", MeshTopology.Triangles);
        // Box 0 to 4 in all dims
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(4, 0, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(4, 4, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 4, 0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 0, 4)));
        mesh.Vertices.Add(new Vertex(new Vector3D(4, 0, 4)));
        mesh.Vertices.Add(new Vertex(new Vector3D(4, 4, 4)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0, 4, 4)));

        // Front face (Z=4)
        mesh.Faces.Add(new Face([4, 5, 6]));
        mesh.Faces.Add(new Face([4, 6, 7]));
        // Back face (Z=0)
        mesh.Faces.Add(new Face([0, 2, 1]));
        mesh.Faces.Add(new Face([0, 3, 2]));

        var result = _converter.ConvertMeshToVoxelGrid(mesh, null, new MeshToVoxelOptions(
            VoxelSize: 1.0,
            Mode: VoxelizationMode.SolidFill));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.GetVoxelCount() > 4);
    }
}

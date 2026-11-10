using System.Text;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.VoxelJson.Services;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Domain.Models;
using NEXUS3D.Voxelization.Engine.Conversion;
using NEXUS3D.Voxelization.Engine.Engine;
using Xunit;

namespace NEXUS3D.Voxelization.Integration.Tests;

public class VoxelizationToVoxelJsonIntegrationTests
{
    private readonly VoxelizationEngine _voxelEngine;
    private readonly VoxelJsonFormatExporter _voxelJsonExporter = new();

    public VoxelizationToVoxelJsonIntegrationTests()
    {
        var validator = new VoxelizationValidator();
        var cuboidConverter = new VoxelToCuboidConverter();
        _voxelEngine = new VoxelizationEngine(validator, cuboidConverter);
    }

    [Fact]
    public async Task VoxelizeMeshAndExportToVoxelJson_ProducesValidJsonOutput()
    {
        // 1. Create a cube mesh
        var cube = new MeshData("JsonVoxelCube");
        cube.Vertices.Add(new Vertex(new Vector3D(0.0, 0.0, 0.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(1.0, 0.0, 0.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(1.0, 1.0, 0.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(0.0, 1.0, 0.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(0.0, 0.0, 1.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(1.0, 0.0, 1.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(1.0, 1.0, 1.0)));
        cube.Vertices.Add(new Vertex(new Vector3D(0.0, 1.0, 1.0)));

        cube.Faces.Add(new Face([0, 3, 2]));
        cube.Faces.Add(new Face([0, 2, 1]));
        cube.Faces.Add(new Face([5, 6, 7]));
        cube.Faces.Add(new Face([5, 7, 4]));
        cube.Faces.Add(new Face([4, 7, 3]));
        cube.Faces.Add(new Face([4, 3, 0]));
        cube.Faces.Add(new Face([1, 2, 6]));
        cube.Faces.Add(new Face([1, 6, 5]));
        cube.Faces.Add(new Face([3, 7, 6]));
        cube.Faces.Add(new Face([3, 6, 2]));
        cube.Faces.Add(new Face([4, 0, 1]));
        cube.Faces.Add(new Face([4, 1, 5]));

        // 2. Voxelize
        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SurfaceOnly,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 8,
            DefaultBlockNamespace = "minecraft:stone"
        };

        var voxelResult = await _voxelEngine.VoxelizeMeshAsync(cube, null, settings);

        Assert.True(voxelResult.IsSuccess);
        Assert.NotNull(voxelResult.VoxelGrid);
        Assert.True(voxelResult.VoxelGrid.GetVoxelCount() > 0);

        // 3. Assemble USM Scene3D
        var scene = new Scene3D { Name = "VoxelJsonScene" };
        var rootNode = new SceneNode("VoxelRoot")
        {
            Object = new SceneObject("VoxelObject")
            {
                VoxelGrids = { voxelResult.VoxelGrid }
            }
        };
        scene.Graph = new SceneGraph(rootNode);

        // 4. Export to Voxel JSON stream
        using var destStream = new MemoryStream();
        var exportReport = await _voxelJsonExporter.ExportSceneAsync(scene, destStream);

        Assert.True(exportReport.IsSuccess);
        Assert.True(destStream.Length > 0);

        string jsonText = Encoding.UTF8.GetString(destStream.ToArray());
        Assert.Contains("voxels", jsonText, StringComparison.OrdinalIgnoreCase);
    }
}

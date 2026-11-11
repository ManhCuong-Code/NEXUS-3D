using System.IO;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.App.Bootstrap;
using NEXUS3D.App.Rendering;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;
using NEXUS3D.Voxelization.Domain.Models;

namespace NEXUS3D.Integration.Tests;

public class ModelLoadingAndViewportAuditTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _testRoot;

    public ModelLoadingAndViewportAuditTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "nexus3d_model_audit_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        var services = new ServiceCollection();
        services.AddNexus3DServices(new DependencyOptions
        {
            CustomAppDataRoot = _testRoot
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch { }
    }

    private static string FindRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "NEXUS-3D.sln")))
            {
                return current;
            }
            current = Path.GetDirectoryName(current)!;
        }
        throw new InvalidOperationException("Could not find repository root containing NEXUS-3D.sln");
    }

    [Fact]
    public void ModelLoadingService_DetectsFormatAccurately_ForCompoundAndSingleExtensions()
    {
        var loader = _serviceProvider.GetRequiredService<IModelLoadingService>();

        var bedrockDesc = loader.DetectFormat("dragon.geo.json");
        Assert.NotNull(bedrockDesc);
        Assert.Equal(ModelFormatType.BedrockGeometry, bedrockDesc.FormatType);

        var voxelDesc = loader.DetectFormat("items.voxel.json");
        Assert.NotNull(voxelDesc);
        Assert.Equal(ModelFormatType.VoxelJson, voxelDesc.FormatType);

        var objDesc = loader.DetectFormat("mesh_model.obj");
        Assert.NotNull(objDesc);
        Assert.Equal(ModelFormatType.Obj, objDesc.FormatType);

        var schemDesc = loader.DetectFormat("castle.schem");
        Assert.NotNull(schemDesc);
        Assert.Equal(ModelFormatType.Schematic, schemDesc.FormatType);

        var schematicDesc = loader.DetectFormat("house.schematic");
        Assert.NotNull(schematicDesc);
        Assert.Equal(ModelFormatType.Schematic, schematicDesc.FormatType);
    }

    [Fact]
    public async Task ModelLoadingService_LoadsBedrockGeometry_Successfully()
    {
        string repoRoot = FindRepositoryRoot();
        string fixturePath = Path.Combine(repoRoot, "tests", "Fixtures", "Json", "bedrock_1_16_0_guardian.geo.json");
        Assert.True(File.Exists(fixturePath), $"Fixture file must exist at {fixturePath}");

        var loader = _serviceProvider.GetRequiredService<IModelLoadingService>();
        var result = await loader.LoadModelAsync(fixturePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.Graph.Root.Children);
    }

    [Fact]
    public async Task ModelLoadingService_LoadsVoxelJson_Successfully()
    {
        string repoRoot = FindRepositoryRoot();
        string fixturePath = Path.Combine(repoRoot, "tests", "Fixtures", "Json", "voxel_cuboid_lantern.json");
        Assert.True(File.Exists(fixturePath), $"Fixture file must exist at {fixturePath}");

        var loader = _serviceProvider.GetRequiredService<IModelLoadingService>();
        var result = await loader.LoadModelAsync(fixturePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task ModelLoadingService_LoadsObj_Successfully()
    {
        string repoRoot = FindRepositoryRoot();
        string fixturePath = Path.Combine(repoRoot, "tests", "Fixtures", "Obj", "TexturedCube.obj");
        Assert.True(File.Exists(fixturePath), $"Fixture file must exist at {fixturePath}");

        var loader = _serviceProvider.GetRequiredService<IModelLoadingService>();
        var result = await loader.LoadModelAsync(fixturePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.Graph.Root.Children);
    }

    [Fact]
    public async Task ModelLoadingService_LoadsSchematic_Successfully()
    {
        string repoRoot = FindRepositoryRoot();
        string fixturePath = Path.Combine(repoRoot, "tests", "Fixtures", "SpongeV2", "small_house_v2.schem");
        Assert.True(File.Exists(fixturePath), $"Fixture file must exist at {fixturePath}");

        var loader = _serviceProvider.GetRequiredService<IModelLoadingService>();
        var result = await loader.LoadModelAsync(fixturePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.Graph.Root.Children);
    }

    [Fact]
    public async Task WorkspaceSceneSession_BroadcastsSceneChanged_WhenModelIsLoaded()
    {
        string repoRoot = FindRepositoryRoot();
        string fixturePath = Path.Combine(repoRoot, "tests", "Fixtures", "Obj", "TexturedCube.obj");

        var session = _serviceProvider.GetRequiredService<IWorkspaceSceneSession>();
        Scene3D? notifiedScene = null;
        string? notifiedPath = null;

        session.SceneChanged += (s, e) =>
        {
            notifiedScene = e.Scene;
            notifiedPath = e.FilePath;
        };

        var result = await session.LoadFileIntoSessionAsync(fixturePath);

        Assert.True(result.IsSuccess);
        Assert.True(session.HasActiveScene);
        Assert.Same(result.Value, session.ActiveScene);
        Assert.Same(result.Value, notifiedScene);
        Assert.Equal(fixturePath, notifiedPath);
        Assert.NotNull(session.ActiveFormat);
        Assert.Equal(ModelFormatType.Obj, session.ActiveFormat.FormatType);
    }

    [Fact]
    public async Task VoxelizationEngine_VoxelizesVoxelGridScene_Successfully()
    {
        var voxelEngine = _serviceProvider.GetRequiredService<IVoxelizationEngine>();

        // Create a scene with only a VoxelGrid (e.g. from Schematic or Voxel JSON)
        var scene = new Scene3D { Name = "SchematicVoxelGridScene" };
        var grid = new VoxelGrid("TestGrid")
        {
            VoxelSize = VoxelSize.UnitCube
        };
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), 1));
        grid.SetVoxel(new Voxel(new VoxelPosition(1, 0, 0), 1));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 1, 0), 1));
        grid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 1), 1));

        var node = new SceneNode("VoxelRoot", new SceneObject("GridObj")
        {
            VoxelGrids = { grid }
        });
        scene.Graph = new SceneGraph(node);

        var settings = new VoxelizationSettings
        {
            Mode = VoxelizationMode.SurfaceOnly,
            ResolutionMode = VoxelResolutionMode.ByMaxGridDimension,
            MaxGridDimension = 32,
            VoxelSize = 1.0
        };

        var result = await voxelEngine.VoxelizeAsync(scene, settings);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.VoxelGrid);
        Assert.True(result.VoxelGrid.GetVoxelCount() > 0, "Output voxel grid must contain rasterized voxels.");
    }

    [Fact]
    public void HelixSceneRenderer_AppliesWorldTransform_ToNodeVisuals()
    {
        using var renderer = new HelixSceneRenderer();

        var scene = new RenderScene { Name = "TransformTestScene" };
        var transform = Matrix4x4.CreateTranslation(15f, 25f, 35f);
        var node = new RenderNode
        {
            Name = "TransformedNode",
            WorldTransform = transform,
            Meshes =
            {
                new RenderMesh
                {
                    Positions = [new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)],
                    Indices = [0, 1, 2]
                }
            }
        };
        scene.RootNodes.Add(node);

        var visual = renderer.BuildSceneVisual(scene, RenderSettings.Default, out _, out _);
        Assert.NotNull(visual);
        Assert.NotNull(visual.Content);

        var rootGroup = Assert.IsType<System.Windows.Media.Media3D.Model3DGroup>(visual.Content);
        Assert.Single(rootGroup.Children);

        var nodeGroup = Assert.IsType<System.Windows.Media.Media3D.Model3DGroup>(rootGroup.Children[0]);
        Assert.NotNull(nodeGroup.Transform);
        var matrixTransform = Assert.IsType<System.Windows.Media.Media3D.MatrixTransform3D>(nodeGroup.Transform);

        Assert.Equal(15.0, matrixTransform.Matrix.OffsetX);
        Assert.Equal(25.0, matrixTransform.Matrix.OffsetY);
        Assert.Equal(35.0, matrixTransform.Matrix.OffsetZ);
    }
}

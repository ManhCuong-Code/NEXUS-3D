using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Batch.DependencyInjection;
using NEXUS3D.Conversion.Engine.DependencyInjection;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.BedrockGeometry.Services;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Formats.Schematic.Services;
using NEXUS3D.Formats.VoxelJson.Services;
using Xunit;

namespace NEXUS3D.Conversion.Integration.Tests;

public sealed class EndToEndConversionPipelineTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUniversalConversionPipeline _pipeline;
    private readonly IBatchConversionService _batchService;
    private readonly string _tempDirectory;

    public EndToEndConversionPipelineTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "NEXUS3D_Conversion_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));

        // Format services (Parsers & Exporters)
        services.AddObjFormatServices();
        services.AddVoxelJsonFormatServices();
        services.AddBedrockGeometryFormatServices();
        services.AddSchematicFormatServices();

        // Conversion Engine & Batch
        services.AddNexusConversionEngine();
        services.AddNexusBatchConversion();

        _serviceProvider = services.BuildServiceProvider();
        _pipeline = _serviceProvider.GetRequiredService<IUniversalConversionPipeline>();
        _batchService = _serviceProvider.GetRequiredService<IBatchConversionService>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static string GetFixturePath(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        string direct = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        if (File.Exists(direct))
        {
            return direct;
        }

        throw new FileNotFoundException($"Fixture file not found: '{relativePath}'");
    }

    [Fact]
    public async Task Convert_VoxelJson_To_Obj_Succeeds_AndGeneratesMesh()
    {
        string sourceFile = GetFixturePath(Path.Combine("tests", "Fixtures", "Json", "voxel_nexus_v1.json"));
        string targetFile = Path.Combine(_tempDirectory, "output_voxel_mesh.obj");

        var request = new ConversionRequest(
            SourceFilePath: sourceFile,
            TargetFilePath: targetFile,
            SourceFormat: ModelFormatType.VoxelJson,
            TargetFormat: ModelFormatType.Obj,
            Options: new ConversionOptions
            {
                MeshingMode = MeshingAlgorithm.GreedyMeshing
            });

        ConversionResult result = await _pipeline.ExecuteAsync(request);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(File.Exists(targetFile), "Target OBJ file was not created.");
        Assert.True(new FileInfo(targetFile).Length > 0, "Target OBJ file is empty.");
        Assert.Equal(ModelFormatType.VoxelJson, result.SourceFormat);
        Assert.Equal(ModelFormatType.Obj, result.TargetFormat);
    }

    [Fact]
    public async Task Convert_Obj_To_VoxelJson_Succeeds_AndGeneratesVoxels()
    {
        string sourceFile = GetFixturePath(Path.Combine("tests", "Fixtures", "Obj", "TexturedCube.obj"));
        string targetFile = Path.Combine(_tempDirectory, "output_cube_voxels.voxel.json");

        var request = new ConversionRequest(
            SourceFilePath: sourceFile,
            TargetFilePath: targetFile,
            SourceFormat: ModelFormatType.Obj,
            TargetFormat: ModelFormatType.VoxelJson,
            Options: new ConversionOptions
            {
                TargetVoxelSize = 0.5,
                VoxelizationMode = VoxelizationMode.SurfaceOnly
            });

        ConversionResult result = await _pipeline.ExecuteAsync(request);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(File.Exists(targetFile), "Target Voxel JSON file was not created.");
        Assert.True(new FileInfo(targetFile).Length > 0, "Target Voxel JSON file is empty.");
    }

    [Fact]
    public async Task Convert_BedrockGeometry_To_Obj_BakesCubesIntoMesh()
    {
        string sourceFile = GetFixturePath(Path.Combine("tests", "Fixtures", "Json", "bedrock_1_16_0_guardian.geo.json"));
        string targetFile = Path.Combine(_tempDirectory, "output_guardian.obj");

        var request = new ConversionRequest(
            SourceFilePath: sourceFile,
            TargetFilePath: targetFile,
            SourceFormat: ModelFormatType.BedrockGeometry,
            TargetFormat: ModelFormatType.Obj,
            Options: new ConversionOptions
            {
                BakeBedrockBonesToMesh = true
            });

        ConversionResult result = await _pipeline.ExecuteAsync(request);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(File.Exists(targetFile), "Target OBJ file was not created.");
        Assert.True(new FileInfo(targetFile).Length > 0, "Target OBJ file is empty.");

        string objText = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("v ", objText);
        Assert.Contains("f ", objText);
    }

    [Fact]
    public async Task Convert_VoxelJson_To_Schematic_Succeeds()
    {
        string sourceFile = GetFixturePath(Path.Combine("tests", "Fixtures", "Json", "voxel_cuboid_lantern.json"));
        string targetFile = Path.Combine(_tempDirectory, "output_lantern.schem");

        var request = new ConversionRequest(
            SourceFilePath: sourceFile,
            TargetFilePath: targetFile,
            SourceFormat: ModelFormatType.VoxelJson,
            TargetFormat: ModelFormatType.Schematic);

        ConversionResult result = await _pipeline.ExecuteAsync(request);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(File.Exists(targetFile), "Target Schematic file was not created.");
        Assert.True(new FileInfo(targetFile).Length > 0, "Target Schematic file is empty.");
    }

    [Fact]
    public async Task BatchConvert_MultipleModels_Concurrently_SucceedsAll()
    {
        string file1 = GetFixturePath(Path.Combine("tests", "Fixtures", "Json", "voxel_nexus_v1.json"));
        string file2 = GetFixturePath(Path.Combine("tests", "Fixtures", "Json", "voxel_cuboid_lantern.json"));
        string file3 = GetFixturePath(Path.Combine("tests", "Fixtures", "Json", "bedrock_1_16_0_guardian.geo.json"));
        string file4 = GetFixturePath(Path.Combine("tests", "Fixtures", "Obj", "TexturedCube.obj"));

        var jobs = new List<ConversionRequest>
        {
            new(file1, Path.Combine(_tempDirectory, "batch_1.obj"), ModelFormatType.VoxelJson, ModelFormatType.Obj),
            new(file2, Path.Combine(_tempDirectory, "batch_2.schem"), ModelFormatType.VoxelJson, ModelFormatType.Schematic),
            new(file3, Path.Combine(_tempDirectory, "batch_3.obj"), ModelFormatType.BedrockGeometry, ModelFormatType.Obj),
            new(file4, Path.Combine(_tempDirectory, "batch_4.voxel.json"), ModelFormatType.Obj, ModelFormatType.VoxelJson)
        };

        var batchRequest = new BatchConversionRequest(jobs, MaxConcurrency: 2, ContinueOnError: true);
        BatchConversionResult batchResult = await _batchService.ConvertBatchAsync(batchRequest);

        Assert.Equal(4, batchResult.TotalJobs);
        Assert.Equal(4, batchResult.SucceededJobs);
        Assert.Equal(0, batchResult.FailedJobs);
        Assert.True(batchResult.TotalDurationMs >= 0);
    }
}

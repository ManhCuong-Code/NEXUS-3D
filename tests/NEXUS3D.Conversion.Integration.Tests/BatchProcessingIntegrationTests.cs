using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Batch.DependencyInjection;
using NEXUS3D.Conversion.Engine.DependencyInjection;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Formats.BedrockGeometry.Services;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Formats.Schematic.Services;
using NEXUS3D.Formats.VoxelJson.Services;
using NEXUS3D.Infrastructure.Persistence;
using NEXUS3D.Infrastructure.Persistence.Repositories;
using Xunit;

namespace NEXUS3D.Conversion.Integration.Tests;

public sealed class BatchProcessingIntegrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBatchProcessingService _batchService;
    private readonly IBatchJobRepository _jobRepository;
    private readonly string _tempDirectory;
    private readonly string _inputDirectory;
    private readonly string _outputDirectory;

    public BatchProcessingIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "NEXUS3D_Batch_Tests_" + Guid.NewGuid().ToString("N"));
        _inputDirectory = Path.Combine(_tempDirectory, "Inputs");
        _outputDirectory = Path.Combine(_tempDirectory, "Outputs");

        Directory.CreateDirectory(_inputDirectory);
        Directory.CreateDirectory(_outputDirectory);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));

        // Format services
        services.AddObjFormatServices();
        services.AddVoxelJsonFormatServices();
        services.AddBedrockGeometryFormatServices();
        services.AddSchematicFormatServices();

        // Database
        string dbPath = Path.Combine(_tempDirectory, "test_batch.db");
        var dbConfig = new DatabaseConfiguration(dbPath);
        services.AddSingleton(dbConfig);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseSchemaMigrator>();
        services.AddSingleton<IBatchJobRepository, SqliteBatchJobRepository>();

        // Conversion Engine & Batch
        services.AddNexusConversionEngine();
        services.AddNexusBatchConversion();

        _serviceProvider = services.BuildServiceProvider();

        // Initialize schema
        var factory = _serviceProvider.GetRequiredService<SqliteConnectionFactory>();
        var migrator = _serviceProvider.GetRequiredService<DatabaseSchemaMigrator>();
        using var connection = factory.CreateOpenConnectionAsync().GetAwaiter().GetResult();
        migrator.MigrateAsync(connection).GetAwaiter().GetResult();

        _batchService = _serviceProvider.GetRequiredService<IBatchProcessingService>();
        _jobRepository = _serviceProvider.GetRequiredService<IBatchJobRepository>();

        CreateTestObjFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, recursive: true); } catch { }
        }
    }

    private void CreateTestObjFiles()
    {
        // Generate 5 simple distinct OBJ cubes
        for (int i = 1; i <= 5; i++)
        {
            string path = Path.Combine(_inputDirectory, $"cube_{i}.obj");
            string content = $"""
                # NEXUS-3D Test Cube {i}
                v 0 0 0
                v {i} 0 0
                v {i} {i} 0
                v 0 {i} 0
                v 0 0 {i}
                v {i} 0 {i}
                v {i} {i} {i}
                v 0 {i} {i}
                f 1 2 3 4
                f 5 6 7 8
                f 1 2 6 5
                f 2 3 7 6
                f 3 4 8 7
                f 4 1 5 8
                """;
            File.WriteAllText(path, content);
        }
    }

    [Fact]
    public async Task BatchProcessing_ObjToSchematic_ParallelExecution_ShouldConvertAll()
    {
        var request = new BatchJobRequest(
            JobName: "ObjToSchematicParallel",
            InputFiles: [],
            InputDirectory: _inputDirectory,
            OutputDirectory: _outputDirectory,
            TargetFormat: ModelFormatType.Schematic,
            Priority: BatchJobPriority.High,
            MaxConcurrency: 4,
            OverwriteExisting: true,
            ExtensionFilter: [".obj"]);

        var job = await _batchService.CreateJobAsync(request);
        Assert.Equal(5, job.TotalFiles);

        var result = await _batchService.StartJobAsync(job.JobId);

        Assert.Equal(BatchJobStatus.Completed, result.Status);
        Assert.Equal(5, result.TotalFiles);
        Assert.Equal(5, result.SucceededFiles);
        Assert.Equal(0, result.FailedFiles);
        Assert.Equal(0, result.SkippedFiles);
        Assert.True(result.DurationMs >= 0);

        // Verify output files exist and are valid schematic files
        for (int i = 1; i <= 5; i++)
        {
            string outPath = Path.Combine(_outputDirectory, $"cube_{i}.schem");
            Assert.True(File.Exists(outPath), $"Expected output file '{outPath}' to exist.");
            Assert.True(new FileInfo(outPath).Length > 0);
        }
    }

    [Fact]
    public async Task BatchProcessing_ObjToVoxelJson_ShouldSucceed()
    {
        string outDir = Path.Combine(_tempDirectory, "JsonOutputs");
        Directory.CreateDirectory(outDir);

        var request = new BatchJobRequest(
            JobName: "ObjToVoxelJson",
            InputFiles: [],
            InputDirectory: _inputDirectory,
            OutputDirectory: outDir,
            TargetFormat: ModelFormatType.VoxelJson,
            Priority: BatchJobPriority.Normal,
            MaxConcurrency: 2,
            OverwriteExisting: true);

        var job = await _batchService.CreateJobAsync(request);
        var result = await _batchService.StartJobAsync(job.JobId);

        Assert.Equal(BatchJobStatus.Completed, result.Status);
        Assert.Equal(5, result.SucceededFiles);

        for (int i = 1; i <= 5; i++)
        {
            string outPath = Path.Combine(outDir, $"cube_{i}.voxel.json");
            Assert.True(File.Exists(outPath));
            string json = File.ReadAllText(outPath);
            Assert.Contains("voxel", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task BatchProcessing_WithCorruptedFile_ContinueOnErrorTrue_ShouldReportPartialFailure()
    {
        string invalidPath = Path.Combine(_inputDirectory, "corrupt.obj");
        File.WriteAllText(invalidPath, "THIS IS NOT A VALID OBJ CONTENT AT ALL");

        string outDir = Path.Combine(_tempDirectory, "PartialOut");
        Directory.CreateDirectory(outDir);

        var request = new BatchJobRequest(
            JobName: "PartialBatchJob",
            InputFiles: [],
            InputDirectory: _inputDirectory,
            OutputDirectory: outDir,
            TargetFormat: ModelFormatType.Schematic,
            ContinueOnError: true,
            RetryLimit: 0);

        var job = await _batchService.CreateJobAsync(request);
        var result = await _batchService.StartJobAsync(job.JobId);

        // 5 valid cubes + 1 corrupt = 6 files
        Assert.Equal(6, result.TotalFiles);
        Assert.Equal(BatchJobStatus.CompletedWithErrors, result.Status);
        Assert.Equal(5, result.SucceededFiles);
        Assert.Equal(1, result.FailedFiles);

        var failedItem = result.Items.FirstOrDefault(i => i.Status == BatchItemStatus.Failed);
        Assert.NotNull(failedItem);
        Assert.Equal("corrupt.obj", Path.GetFileName(failedItem.SourceFilePath));
    }

    [Fact]
    public async Task BatchProcessing_OverwritePolicy_ShouldSkipExistingFilesWhenDisabled()
    {
        string outDir = Path.Combine(_tempDirectory, "SkipOutputs");
        Directory.CreateDirectory(outDir);

        // Pre-create cube_1.schem
        string preExisting = Path.Combine(outDir, "cube_1.schem");
        File.WriteAllText(preExisting, "EXISTING_PRESERVED_FILE");

        var request = new BatchJobRequest(
            JobName: "SkipPolicyJob",
            InputFiles: [],
            InputDirectory: _inputDirectory,
            OutputDirectory: outDir,
            TargetFormat: ModelFormatType.Schematic,
            OverwriteExisting: false);

        var job = await _batchService.CreateJobAsync(request);
        var result = await _batchService.StartJobAsync(job.JobId);

        Assert.Equal(1, result.SkippedFiles);
        Assert.Equal(4, result.SucceededFiles);

        // Verify pre-existing file content wasn't overwritten
        Assert.Equal("EXISTING_PRESERVED_FILE", File.ReadAllText(preExisting));
    }

    [Fact]
    public async Task BatchProcessing_Cancellation_ShouldHaltExecution()
    {
        string outDir = Path.Combine(_tempDirectory, "CancelOutputs");
        Directory.CreateDirectory(outDir);

        var request = new BatchJobRequest(
            JobName: "CancellableJob",
            InputFiles: [],
            InputDirectory: _inputDirectory,
            OutputDirectory: outDir,
            TargetFormat: ModelFormatType.Schematic,
            MaxConcurrency: 1);

        var job = await _batchService.CreateJobAsync(request);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Ensure cancellation token is triggered

        var result = await _batchService.StartJobAsync(job.JobId, cancellationToken: cts.Token);

        Assert.Equal(BatchJobStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task BatchProcessing_SqlitePersistence_ShouldSaveAndRetrieveJobHistory()
    {
        string outDir = Path.Combine(_tempDirectory, "PersistOutputs");
        Directory.CreateDirectory(outDir);

        var request = new BatchJobRequest(
            JobName: "PersistentBatchJob",
            InputFiles: [],
            InputDirectory: _inputDirectory,
            OutputDirectory: outDir,
            TargetFormat: ModelFormatType.Schematic,
            Priority: BatchJobPriority.Critical);

        var job = await _batchService.CreateJobAsync(request);
        await _batchService.StartJobAsync(job.JobId);

        // Retrieve from repository directly
        var record = await _jobRepository.GetJobByIdAsync(job.JobId);

        Assert.NotNull(record);
        Assert.Equal(job.JobId, record.Id);
        Assert.Equal("PersistentBatchJob", record.JobName);
        Assert.Equal(BatchJobStatus.Completed, record.Status);
        Assert.Equal(BatchJobPriority.Critical, record.Priority);
        Assert.Equal(5, record.TotalFiles);
        Assert.Equal(5, record.SucceededFiles);
        Assert.Equal(5, record.Items.Count);

        var recent = await _jobRepository.GetRecentJobsAsync(10);
        Assert.NotEmpty(recent);
        Assert.Contains(recent, r => r.Id == job.JobId);
    }
}

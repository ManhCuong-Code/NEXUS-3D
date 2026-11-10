using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Batch.DependencyInjection;
using NEXUS3D.Conversion.Engine.DependencyInjection;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Obj;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Infrastructure.Formats;
using NEXUS3D.Infrastructure.Persistence;
using NEXUS3D.Infrastructure.Persistence.Repositories;
using NEXUS3D.Sample.Plugin;
using Nexus3D.CLI;
using Nexus3D.CLI.Arguments;
using Nexus3D.CLI.Commands;
using Xunit;

namespace NEXUS3D.Plugin.Integration.Tests;

public sealed class PluginBatchAndCliIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;

    public PluginBatchAndCliIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "Nexus3D_CliBatchTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void CliArgumentParser_PluginsCommands_ParsedAccurately()
    {
        var listArgs = CliArgumentParser.Parse(["plugins", "list"]);
        Assert.Equal(CliCommandType.PluginsList, listArgs.Command);

        var infoArgs = CliArgumentParser.Parse(["plugins", "info", "com.nexus3d.formats.rawvox"]);
        Assert.Equal(CliCommandType.PluginsInfo, infoArgs.Command);
        Assert.Equal("com.nexus3d.formats.rawvox", infoArgs.PluginId);

        var formatsArgs = CliArgumentParser.Parse(["formats", "list"]);
        Assert.Equal(CliCommandType.FormatsList, formatsArgs.Command);
    }

    [Fact]
    public void FormatsCommandHandler_Executes_ReturnsSuccess()
    {
        var formatRegistry = new FormatRegistry();
        formatRegistry.RegisterDescriptor(new ObjFormatDescriptor());
        formatRegistry.RegisterDescriptor(new RawVoxFormatDescriptor());

        var handler = new FormatsCommandHandler(formatRegistry);

        int exitCode = handler.Execute();
        Assert.Equal(CliExitCodes.Success, exitCode);
    }

    [Fact]
    public async Task BatchProcessing_WithRawVoxInputs_ConvertsSuccessfully()
    {
        // 1. Setup DI container for Batch Processing with registered RawVox services
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());

        string dbPath = Path.Combine(_tempDirectory, "batch_test.db");
        services.AddSingleton(new DatabaseConfiguration(dbPath));
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseSchemaMigrator>();
        services.AddSingleton<IBatchJobRepository, SqliteBatchJobRepository>();

        var formatRegistry = new FormatRegistry();
        formatRegistry.RegisterDescriptor(new ObjFormatDescriptor());
        formatRegistry.RegisterDescriptor(new RawVoxFormatDescriptor());
        services.AddSingleton<IFormatRegistry>(formatRegistry);

        services.AddObjFormatServices();
        services.AddSingleton<IFormatParser, RawVoxParser>();
        services.AddSingleton<IFormatExporter, RawVoxExporter>();

        services.AddNexusConversionEngine();
        services.AddNexusBatchConversion();

        var sp = services.BuildServiceProvider();

        // Migrate DB
        var factory = sp.GetRequiredService<SqliteConnectionFactory>();
        var migrator = sp.GetRequiredService<DatabaseSchemaMigrator>();
        using (var connection = await factory.CreateOpenConnectionAsync())
        {
            await migrator.MigrateAsync(connection);
        }

        // 2. Create rawvox file
        string inputDir = Path.Combine(_tempDirectory, "Input");
        string outputDir = Path.Combine(_tempDirectory, "Output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        string rawvoxFile = Path.Combine(inputDir, "sample.rawvox");
        await File.WriteAllTextAsync(rawvoxFile, """
        RAWVOX1
        NAME Sample
        DIMENSIONS 1 1 1
        PALETTE 1
        0 minecraft:stone
        VOXELS 1
        0 0 0 0
        VERTICES 3
        0.0 0.0 0.0
        1.0 0.0 0.0
        0.0 1.0 0.0
        FACES 1
        0 1 2
        """);

        // 3. Run Batch Processing Service
        var batchService = sp.GetRequiredService<IBatchProcessingService>();
        var request = new BatchJobRequest(
            JobName: "RawVoxBatchJob",
            InputFiles: [],
            InputDirectory: inputDir,
            OutputDirectory: outputDir,
            TargetFormat: ModelFormatType.Obj,
            Priority: BatchJobPriority.Normal,
            MaxConcurrency: 2,
            OverwriteExisting: true,
            ExtensionFilter: [".rawvox"]);

        var job = await batchService.CreateJobAsync(request);
        Assert.Equal(1, job.TotalFiles);

        var batchResult = await batchService.StartJobAsync(job.JobId);

        Assert.Equal(BatchJobStatus.Completed, batchResult.Status);
        Assert.Equal(1, batchResult.TotalFiles);
        Assert.Equal(1, batchResult.SucceededFiles);
        Assert.Equal(0, batchResult.FailedFiles);

        string expectedOutputFile = Path.Combine(outputDir, "sample.obj");
        Assert.True(File.Exists(expectedOutputFile));
    }
}

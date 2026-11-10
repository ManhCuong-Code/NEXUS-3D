using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Conversion.Batch.DependencyInjection;
using NEXUS3D.Conversion.Engine.DependencyInjection;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.BedrockGeometry.Services;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Formats.Schematic.Services;
using NEXUS3D.Formats.VoxelJson.Services;
using NEXUS3D.Infrastructure.FileSystem;
using NEXUS3D.Infrastructure.Formats;
using NEXUS3D.Infrastructure.Persistence;
using NEXUS3D.Infrastructure.Persistence.Repositories;
using NEXUS3D.Voxelization.Engine.DependencyInjection;
using Nexus3D.CLI.Commands;

namespace Nexus3D.CLI;

/// <summary>
/// Configures dependency injection container for headless CLI operations.
/// </summary>
public static class CliBootstrapper
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        // Application paths & Database
        var paths = new ApplicationPathsService();
        services.AddSingleton<IApplicationPathsService>(paths);
        paths.EnsureDirectoriesExist();

        var dbConfig = new DatabaseConfiguration(paths.DatabaseFilePath);
        services.AddSingleton(dbConfig);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseSchemaMigrator>();
        services.AddSingleton<IBatchJobRepository, SqliteBatchJobRepository>();

        // Format Parsers & Exporters
        services.AddSchematicFormatServices();
        services.AddObjFormatServices();
        services.AddVoxelJsonFormatServices();
        services.AddBedrockGeometryFormatServices();
        services.AddSingleton<IFormatRegistry, FormatRegistry>();

        // Conversion Engine & Voxelization & Batch
        services.AddNexusConversionEngine();
        services.AddNexusVoxelization();
        services.AddNexusBatchConversion();

        // CLI Command Handlers
        services.AddTransient<ConvertCommandHandler>();
        services.AddTransient<BatchCommandHandler>();
        services.AddTransient<JobsCommandHandler>();

        var provider = services.BuildServiceProvider();

        // Initialize SQLite schema
        try
        {
            var factory = provider.GetRequiredService<SqliteConnectionFactory>();
            var migrator = provider.GetRequiredService<DatabaseSchemaMigrator>();
            using var connection = factory.CreateOpenConnectionAsync().GetAwaiter().GetResult();
            migrator.MigrateAsync(connection).GetAwaiter().GetResult();
        }
        catch
        {
            // Fallback for transient DB lock or file permission in CLI
        }

        return provider;
    }
}

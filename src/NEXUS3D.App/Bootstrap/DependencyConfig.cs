using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS3D.App.Navigation;
using NEXUS3D.App.ViewModels;
using NEXUS3D.App.Views;
using NEXUS3D.Application.DependencyInjection;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.UseCases;
using NEXUS3D.Conversion.Batch.DependencyInjection;
using NEXUS3D.Conversion.Engine.DependencyInjection;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Engine.Contracts;
using NEXUS3D.Engine.Services;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.BedrockGeometry;
using NEXUS3D.Formats.BedrockGeometry.Services;
using NEXUS3D.Formats.Obj;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Formats.Schematic;
using NEXUS3D.Formats.Schematic.Services;
using NEXUS3D.Formats.VoxelJson;
using NEXUS3D.Formats.VoxelJson.Services;
using NEXUS3D.Infrastructure.Caching;
using NEXUS3D.Infrastructure.Configuration;
using NEXUS3D.Infrastructure.DependencyInjection;
using NEXUS3D.Infrastructure.FileSystem;
using NEXUS3D.Infrastructure.Formats;
using NEXUS3D.Infrastructure.Logging;
using NEXUS3D.Infrastructure.Persistence;
using NEXUS3D.Infrastructure.Persistence.Repositories;
using NEXUS3D.PluginSystem.Contracts;
using NEXUS3D.PluginSystem.Services;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.DependencyInjection;
using NEXUS3D.Rendering.Services;
using NEXUS3D.Voxelization.Engine.DependencyInjection;

namespace NEXUS3D.App.Bootstrap;

/// <summary>
/// Options for configuring NEXUS 3D Dependency Injection (supports overriding paths in integration tests).
/// </summary>
public sealed class DependencyOptions
{
    public string? CustomAppDataRoot { get; init; }
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Information;
}

/// <summary>
/// Modular dependency configuration for Infrastructure, Formats, Engine, Application, and Presentation layers.
/// </summary>
public static class DependencyConfig
{
    public static void ConfigureInfrastructure(IServiceCollection services, DependencyOptions options)
    {
        var pathsService = new ApplicationPathsService(options.CustomAppDataRoot);
        pathsService.EnsureDirectoriesExist();

        services.AddSingleton<IApplicationPathsService>(pathsService);
        services.AddSingleton<InMemoryCacheService>();
        services.AddSingleton<JsonConfigurationStore>();

        var logConfig = new LogConfiguration
        {
            LogDirectory = pathsService.LogsDirectory,
            MinimumLevel = options.MinimumLogLevel,
            RedactSensitiveData = true
        };

        services.AddSingleton(logConfig);
        services.AddSingleton<LogFileProvider>();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(options.MinimumLogLevel);
            builder.AddDebug();
            builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<LogFileProvider>());
        });

        services.AddSingleton<ExceptionLogger>();

        var dbConfig = new DatabaseConfiguration(pathsService.DatabaseFilePath);
        services.AddSingleton(dbConfig);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseSchemaMigrator>();

        services.AddSingleton<IConversionHistoryRepository, SqliteConversionHistoryRepository>();
        services.AddSingleton<IRecentFileRepository, SqliteRecentFileRepository>();
        services.AddSingleton<IConversionProfileRepository, SqliteConversionProfileRepository>();
        services.AddSingleton<IExportConfigurationRepository, SqliteExportConfigurationRepository>();
        services.AddSingleton<IAppSettingRepository, SqliteAppSettingRepository>();
        services.AddSingleton<IPluginRegistrationRepository, SqlitePluginRegistrationRepository>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddUsmInfrastructureServices();
    }

    public static void ConfigureFormatsAndEngine(IServiceCollection services)
    {
        services.AddSingleton<IFormatDescriptor, SchematicFormatDescriptor>();
        services.AddSchematicFormatServices();
        services.AddSingleton<IFormatDescriptor, ObjFormatDescriptor>();
        services.AddObjFormatServices();
        services.AddSingleton<IFormatDescriptor, VoxelJsonFormatDescriptor>();
        services.AddVoxelJsonFormatServices();
        services.AddSingleton<IFormatDescriptor, BedrockGeometryFormatDescriptor>();
        services.AddBedrockGeometryFormatServices();
        services.AddSingleton<IFormatRegistry, FormatRegistry>();

        services.AddNexusConversionEngine();
        services.AddNexusBatchConversion();
        services.AddNexusRendering();
        services.AddNexusVoxelization();

        services.AddSingleton<IConversionPipeline, FoundationConversionPipeline>();
        services.AddSingleton<IPluginCatalogService, PluginCatalogService>();
    }

    public static void ConfigureApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<IConverterPreparationService, ConverterPreparationService>();
        services.AddSingleton<IFileManagerService, FileManagerService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddUsmApplicationServices();
    }

    public static void ConfigurePresentation(IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<CameraViewModel>();
        services.AddSingleton<RenderSettingsViewModel>();
        services.AddSingleton<SceneHierarchyViewModel>();
        services.AddSingleton<ObjectInspectorViewModel>();
        services.AddSingleton<BoneHierarchyViewModel>();
        services.AddSingleton<ViewportViewModel>();
        services.AddSingleton<VoxelizationViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ConverterViewModel>();
        services.AddSingleton<FileManagerViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddTransient<MainWindow>();
    }
}


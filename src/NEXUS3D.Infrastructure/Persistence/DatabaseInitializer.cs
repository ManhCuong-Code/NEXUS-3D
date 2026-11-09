using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Infrastructure.Persistence;

/// <summary>
/// Initializes the SQLite database, executes pending migrations, and seeds baseline foundation profiles/settings.
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseSchemaMigrator _migrator;
    private readonly IConversionProfileRepository _profileRepository;
    private readonly IExportConfigurationRepository _exportConfigRepository;
    private readonly IAppSettingRepository _settingRepository;
    private readonly IPluginRegistrationRepository _pluginRepository;
    private readonly IApplicationPathsService _pathsService;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        SqliteConnectionFactory connectionFactory,
        DatabaseSchemaMigrator migrator,
        IConversionProfileRepository profileRepository,
        IExportConfigurationRepository exportConfigRepository,
        IAppSettingRepository settingRepository,
        IPluginRegistrationRepository pluginRepository,
        IApplicationPathsService pathsService,
        ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = Guard.NotNull(connectionFactory);
        _migrator = Guard.NotNull(migrator);
        _profileRepository = Guard.NotNull(profileRepository);
        _exportConfigRepository = Guard.NotNull(exportConfigRepository);
        _settingRepository = Guard.NotNull(settingRepository);
        _pluginRepository = Guard.NotNull(pluginRepository);
        _pathsService = Guard.NotNull(pathsService);
        _logger = Guard.NotNull(logger);
    }

    public async Task<OperationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _pathsService.EnsureDirectoriesExist();

            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            int schemaVersion = await _migrator.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);

            await SeedDefaultsIfEmptyAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "SQLite database initialized at '{DbPath}' (Schema Version: {SchemaVersion}).",
                _connectionFactory.Configuration.DatabaseFilePath,
                schemaVersion);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize SQLite database.");
            return OperationResult.Failure("DB_INIT_FAILED", ex.Message);
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            object? scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(scalar) == 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQLite health check failed.");
            return false;
        }
    }

    private async Task SeedDefaultsIfEmptyAsync(CancellationToken cancellationToken)
    {
        var profiles = await _profileRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (profiles.Count == 0)
        {
            var builtInProfiles = new[]
            {
                new ConversionProfile(
                    name: "Schematic to OBJ (Greedy Mesh)",
                    description: "Optimized polygon mesh export from Minecraft Schematic voxel grids.",
                    sourceFormat: ModelFormatType.Schematic,
                    targetFormat: ModelFormatType.Obj,
                    scaleFactor: 1.0,
                    optimizeMesh: true,
                    greedyMeshing: true,
                    preserveMaterials: true,
                    isBuiltIn: true),
                new ConversionProfile(
                    name: "Voxel JSON to Bedrock Geometry",
                    description: "Converts Java block/item cuboid JSON to Bedrock .geo.json hierarchy.",
                    sourceFormat: ModelFormatType.VoxelJson,
                    targetFormat: ModelFormatType.BedrockGeometry,
                    scaleFactor: 1.0,
                    optimizeMesh: false,
                    greedyMeshing: false,
                    preserveMaterials: true,
                    isBuiltIn: true),
                new ConversionProfile(
                    name: "OBJ Mesh to Schematic Voxelizer",
                    description: "Prepares polygon meshes for voxel grid quantization.",
                    sourceFormat: ModelFormatType.Obj,
                    targetFormat: ModelFormatType.Schematic,
                    scaleFactor: 16.0,
                    optimizeMesh: true,
                    greedyMeshing: true,
                    preserveMaterials: true,
                    isBuiltIn: true),
                new ConversionProfile(
                    name: "Bedrock Geometry to OBJ Mesh",
                    description: "Exports Bedrock bone/cube hierarchies to standard Wavefront OBJ.",
                    sourceFormat: ModelFormatType.BedrockGeometry,
                    targetFormat: ModelFormatType.Obj,
                    scaleFactor: 0.0625,
                    optimizeMesh: true,
                    greedyMeshing: false,
                    preserveMaterials: true,
                    isBuiltIn: true)
            };

            foreach (var profile in builtInProfiles)
            {
                await _profileRepository.UpsertAsync(profile, cancellationToken).ConfigureAwait(false);
            }
        }

        var defaultExport = await _exportConfigRepository.GetDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (defaultExport is null)
        {
            string exportsFolder = Path.Combine(_pathsService.AppDataRootDirectory, "Exports");
            var config = new ExportConfiguration(
                profileName: "Default Export",
                targetFormat: ModelFormatType.Obj,
                outputDirectory: exportsFolder,
                overwriteExisting: false,
                generateMtlFile: true,
                compressOutput: false,
                coordinateSystem: CoordinateAxisUp.YUp,
                decimalPrecision: 4);

            await _exportConfigRepository.UpsertAsync(config, cancellationToken).ConfigureAwait(false);
        }

        var existingSettings = await _settingRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (existingSettings.Count == 0)
        {
            await _settingRepository.SetValueAsync("UI.Theme", "Dark Industrial", "UI", "Active visual theme", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Export.DefaultOutputDirectory", Path.Combine(_pathsService.AppDataRootDirectory, "Exports"), "Export", "Default output directory", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Export.DefaultTargetFormat", ModelFormatType.Obj.ToString(), "Export", "Default target format", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Geometry.DefaultAxisUp", CoordinateAxisUp.YUp.ToString(), "Geometry", "Default coordinate system up-axis", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Engine.AutoOptimizeMesh", "True", "Engine", "Automatically optimize mesh", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Logging.DetailedFileLog", "True", "Logging", "Enable detailed file logs", cancellationToken).ConfigureAwait(false);
        }

        var plugins = await _pluginRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (plugins.Count == 0)
        {
            var coreAdapters = new[]
            {
                new PluginRegistration(
                    pluginId: "nexus3d.formats.schematic",
                    name: "NEXUS Schematic Format Adapter",
                    version: "0.1.0",
                    author: "NEXUS 3D Core Team",
                    description: "Built-in format descriptor for .schem, .schematic, and .litematic files.",
                    assemblyPath: "NEXUS3D.Formats.Schematic.dll",
                    supportedFormats: "Schematic",
                    isEnabled: true),
                new PluginRegistration(
                    pluginId: "nexus3d.formats.obj",
                    name: "NEXUS Wavefront OBJ Adapter",
                    version: "0.1.0",
                    author: "NEXUS 3D Core Team",
                    description: "Built-in format descriptor for Wavefront .obj and .mtl polygon meshes.",
                    assemblyPath: "NEXUS3D.Formats.Obj.dll",
                    supportedFormats: "Obj",
                    isEnabled: true),
                new PluginRegistration(
                    pluginId: "nexus3d.formats.voxeljson",
                    name: "NEXUS Voxel JSON Adapter",
                    version: "0.1.0",
                    author: "NEXUS 3D Core Team",
                    description: "Built-in format descriptor for Java Voxel Cuboid .json models.",
                    assemblyPath: "NEXUS3D.Formats.VoxelJson.dll",
                    supportedFormats: "VoxelJson",
                    isEnabled: true),
                new PluginRegistration(
                    pluginId: "nexus3d.formats.bedrock",
                    name: "NEXUS Bedrock Geometry Adapter",
                    version: "0.1.0",
                    author: "NEXUS 3D Core Team",
                    description: "Built-in format descriptor for Bedrock Engine .geo.json models.",
                    assemblyPath: "NEXUS3D.Formats.BedrockGeometry.dll",
                    supportedFormats: "BedrockGeometry",
                    isEnabled: true)
            };

            foreach (var plugin in coreAdapters)
            {
                await _pluginRepository.UpsertAsync(plugin, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}


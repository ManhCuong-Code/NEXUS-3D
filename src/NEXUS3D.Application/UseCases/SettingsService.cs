using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.UseCases;

/// <summary>
/// Manages user preferences and default export configurations stored in SQLite.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IAppSettingRepository _settingRepository;
    private readonly IExportConfigurationRepository _exportConfigRepository;
    private readonly IApplicationPathsService _pathsService;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(
        IAppSettingRepository settingRepository,
        IExportConfigurationRepository exportConfigRepository,
        IApplicationPathsService pathsService,
        ILogger<SettingsService> logger)
    {
        _settingRepository = Guard.NotNull(settingRepository);
        _exportConfigRepository = Guard.NotNull(exportConfigRepository);
        _pathsService = Guard.NotNull(pathsService);
        _logger = Guard.NotNull(logger);
    }

    public async Task<OperationResult<ApplicationPreferencesDto>> LoadPreferencesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var map = settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);
            var defaultExport = await _exportConfigRepository.GetDefaultAsync(cancellationToken).ConfigureAwait(false);

            string defaultOutputDir = map.GetValueOrDefault("Export.DefaultOutputDirectory")
                ?? defaultExport?.OutputDirectory
                ?? Path.Combine(_pathsService.AppDataRootDirectory, "Exports");

            string themeMode = map.GetValueOrDefault("UI.Theme") ?? "Dark Industrial";

            ModelFormatType targetFormat = defaultExport?.TargetFormat ?? ModelFormatType.Obj;
            if (map.TryGetValue("Export.DefaultTargetFormat", out string? fmtStr) &&
                Enum.TryParse<ModelFormatType>(fmtStr, ignoreCase: true, out var parsedFormat) &&
                parsedFormat != ModelFormatType.Unknown)
            {
                targetFormat = parsedFormat;
            }

            CoordinateAxisUp axisUp = defaultExport?.CoordinateSystem ?? CoordinateAxisUp.YUp;
            if (map.TryGetValue("Geometry.DefaultAxisUp", out string? axisStr) &&
                Enum.TryParse<CoordinateAxisUp>(axisStr, ignoreCase: true, out var parsedAxis))
            {
                axisUp = parsedAxis;
            }

            bool autoOptimize = !map.TryGetValue("Engine.AutoOptimizeMesh", out string? optStr) ||
                                !bool.TryParse(optStr, out bool parsedOpt) || parsedOpt;

            bool detailedLog = !map.TryGetValue("Logging.DetailedFileLog", out string? logStr) ||
                               !bool.TryParse(logStr, out bool parsedLog) || parsedLog;

            int maxRecent = 25;
            if (map.TryGetValue("Workspace.MaxRecentFiles", out string? recentStr) &&
                int.TryParse(recentStr, out int parsedRecent))
            {
                maxRecent = Math.Clamp(parsedRecent, 5, 100);
            }

            int precision = defaultExport?.DecimalPrecision ?? 4;
            if (map.TryGetValue("Export.DecimalPrecision", out string? precStr) &&
                int.TryParse(precStr, out int parsedPrec))
            {
                precision = Math.Clamp(parsedPrec, 1, 10);
            }

            bool createDesktopShortcut = map.TryGetValue("Workspace.CreateDesktopShortcut", out string? shortcutStr) &&
                                         bool.TryParse(shortcutStr, out bool parsedShortcut) && parsedShortcut;

            var dto = new ApplicationPreferencesDto(
                ThemeMode: themeMode,
                DefaultOutputDirectory: defaultOutputDir,
                DefaultTargetFormat: targetFormat,
                DefaultAxisUp: axisUp,
                AutoOptimizeMesh: autoOptimize,
                EnableDetailedFileLogging: detailedLog,
                MaxRecentFiles: maxRecent,
                DecimalPrecision: precision,
                CreateDesktopShortcut: createDesktopShortcut);

            return OperationResult<ApplicationPreferencesDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load application preferences.");
            return OperationResult<ApplicationPreferencesDto>.Failure("SETTINGS_LOAD_ERR", ex.Message);
        }
    }

    public async Task<OperationResult> SavePreferencesAsync(
        ApplicationPreferencesDto preferences,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(preferences);

        try
        {
            await _settingRepository.SetValueAsync("UI.Theme", preferences.ThemeMode, "UI", "Active visual theme", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Export.DefaultOutputDirectory", preferences.DefaultOutputDirectory, "Export", "Default directory for converted models", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Export.DefaultTargetFormat", preferences.DefaultTargetFormat.ToString(), "Export", "Default target format", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Geometry.DefaultAxisUp", preferences.DefaultAxisUp.ToString(), "Geometry", "Default coordinate system up-axis", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Engine.AutoOptimizeMesh", preferences.AutoOptimizeMesh.ToString(), "Engine", "Automatically optimize mesh during conversion", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Logging.DetailedFileLog", preferences.EnableDetailedFileLogging.ToString(), "Logging", "Write detailed diagnostic logs to disk", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Workspace.MaxRecentFiles", Math.Clamp(preferences.MaxRecentFiles, 5, 100).ToString(), "Workspace", "Maximum recent files tracked", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Export.DecimalPrecision", Math.Clamp(preferences.DecimalPrecision, 1, 10).ToString(), "Export", "Floating-point precision for exported geometry", cancellationToken).ConfigureAwait(false);
            await _settingRepository.SetValueAsync("Workspace.CreateDesktopShortcut", preferences.CreateDesktopShortcut.ToString(), "Workspace", "Automatically maintain desktop shortcut", cancellationToken).ConfigureAwait(false);

            var existingExport = await _exportConfigRepository.GetDefaultAsync(cancellationToken).ConfigureAwait(false);
            var updatedExport = new ExportConfiguration(
                profileName: "Default Export",
                targetFormat: preferences.DefaultTargetFormat,
                outputDirectory: preferences.DefaultOutputDirectory,
                overwriteExisting: existingExport?.OverwriteExisting ?? false,
                generateMtlFile: existingExport?.GenerateMtlFile ?? true,
                compressOutput: existingExport?.CompressOutput ?? false,
                coordinateSystem: preferences.DefaultAxisUp,
                decimalPrecision: Math.Clamp(preferences.DecimalPrecision, 1, 10))
            {
                Id = existingExport?.Id ?? Guid.NewGuid()
            };

            await _exportConfigRepository.UpsertAsync(updatedExport, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Application preferences saved successfully.");
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save application preferences.");
            return OperationResult.Failure("SETTINGS_SAVE_ERR", ex.Message);
        }
    }

    public async Task<OperationResult<ApplicationPreferencesDto>> ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new ApplicationPreferencesDto(
            ThemeMode: "Dark Industrial",
            DefaultOutputDirectory: Path.Combine(_pathsService.AppDataRootDirectory, "Exports"),
            DefaultTargetFormat: ModelFormatType.Obj,
            DefaultAxisUp: CoordinateAxisUp.YUp,
            AutoOptimizeMesh: true,
            EnableDetailedFileLogging: true,
            MaxRecentFiles: 25,
            DecimalPrecision: 4,
            CreateDesktopShortcut: false);

        var saveResult = await SavePreferencesAsync(defaults, cancellationToken).ConfigureAwait(false);
        return saveResult.IsSuccess
            ? OperationResult<ApplicationPreferencesDto>.Success(defaults)
            : OperationResult<ApplicationPreferencesDto>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
    }
}


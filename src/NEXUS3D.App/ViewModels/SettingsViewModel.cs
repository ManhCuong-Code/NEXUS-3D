using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.PluginSystem.Contracts;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel for the Application Settings and Plugin Configuration screen.
/// </summary>
public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IPluginCatalogService _pluginCatalogService;
    private readonly INotificationService _notificationService;
    private readonly IApplicationPathsService _pathsService;

    [ObservableProperty]
    private string _themeMode = "Dark Industrial";

    [ObservableProperty]
    private string _defaultOutputDirectory = string.Empty;

    [ObservableProperty]
    private ModelFormatType _defaultTargetFormat = ModelFormatType.Obj;

    [ObservableProperty]
    private CoordinateAxisUp _defaultAxisUp = CoordinateAxisUp.YUp;

    [ObservableProperty]
    private bool _autoOptimizeMesh = true;

    [ObservableProperty]
    private bool _enableDetailedFileLogging = true;

    [ObservableProperty]
    private int _maxRecentFiles = 25;

    [ObservableProperty]
    private int _decimalPrecision = 4;

    public string AppDataDirectory => _pathsService.AppDataRootDirectory;
    public string DatabaseFilePath => _pathsService.DatabaseFilePath;
    public string LogsDirectory => _pathsService.LogsDirectory;

    public ObservableCollection<string> AvailableThemes { get; } =
    [
        "Dark Industrial",
        "Graphite Studio"
    ];

    public ObservableCollection<ModelFormatType> AvailableTargetFormats { get; } =
    [
        ModelFormatType.Obj,
        ModelFormatType.Schematic,
        ModelFormatType.VoxelJson,
        ModelFormatType.BedrockGeometry
    ];

    public ObservableCollection<CoordinateAxisUp> AvailableAxisConventions { get; } =
    [
        CoordinateAxisUp.YUp,
        CoordinateAxisUp.ZUp
    ];

    public ObservableCollection<PluginRegistration> RegisteredPlugins { get; } = [];

    public KeyboardShortcutViewModel Shortcuts { get; }

    public SettingsViewModel(
        ISettingsService settingsService,
        IPluginCatalogService pluginCatalogService,
        INotificationService notificationService,
        IApplicationPathsService pathsService,
        KeyboardShortcutViewModel shortcutsViewModel)
    {
        _settingsService = Guard.NotNull(settingsService);
        _pluginCatalogService = Guard.NotNull(pluginCatalogService);
        _notificationService = Guard.NotNull(notificationService);
        _pathsService = Guard.NotNull(pathsService);
        Shortcuts = Guard.NotNull(shortcutsViewModel);

        Title = "System Settings & Configuration";
        Subtitle = "Configure default export parameters, coordinate conventions, logging, format plugins, and keyboard shortcuts.";
    }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken = default) =>
        LoadSettingsAsync(cancellationToken);

    [RelayCommand]
    private async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var result = await _settingsService.LoadPreferencesAsync(cancellationToken);
            if (result.IsSuccess && result.Value is not null)
            {
                ApplyDto(result.Value);
            }
            else if (result.IsFailure)
            {
                ErrorMessage = result.ErrorMessage;
            }

            var plugins = await _pluginCatalogService.GetRegisteredPluginsAsync(cancellationToken);
            RegisteredPlugins.Clear();
            foreach (var plugin in plugins)
            {
                RegisteredPlugins.Add(plugin);
            }

            StatusMessage = "Configuration loaded from SQLite.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();

            var dto = new ApplicationPreferencesDto(
                ThemeMode: ThemeMode,
                DefaultOutputDirectory: DefaultOutputDirectory,
                DefaultTargetFormat: DefaultTargetFormat,
                DefaultAxisUp: DefaultAxisUp,
                AutoOptimizeMesh: AutoOptimizeMesh,
                EnableDetailedFileLogging: EnableDetailedFileLogging,
                MaxRecentFiles: MaxRecentFiles,
                DecimalPrecision: DecimalPrecision);

            var result = await _settingsService.SavePreferencesAsync(dto, cancellationToken);
            if (result.IsFailure)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            foreach (var plugin in RegisteredPlugins)
            {
                await _pluginCatalogService.SetPluginStateAsync(plugin.PluginId, plugin.IsEnabled, cancellationToken);
            }

            StatusMessage = "Preferences saved successfully.";
            _notificationService.Publish(
                "Settings Saved",
                "Application preferences and export defaults have been updated.",
                NotificationSeverity.Success,
                "Settings");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _settingsService.ResetToDefaultsAsync(cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            ApplyDto(result.Value);
            StatusMessage = "Restored factory default settings.";
            _notificationService.Publish(
                "Defaults Restored",
                "All preferences were reset to default values.",
                NotificationSeverity.Info,
                "Settings");
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }
    }

    private void ApplyDto(ApplicationPreferencesDto dto)
    {
        ThemeMode = dto.ThemeMode;
        DefaultOutputDirectory = dto.DefaultOutputDirectory;
        DefaultTargetFormat = dto.DefaultTargetFormat;
        DefaultAxisUp = dto.DefaultAxisUp;
        AutoOptimizeMesh = dto.AutoOptimizeMesh;
        EnableDetailedFileLogging = dto.EnableDetailedFileLogging;
        MaxRecentFiles = dto.MaxRecentFiles;
        DecimalPrecision = dto.DecimalPrecision;
    }
}


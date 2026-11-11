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
    private readonly IDesktopShortcutService _desktopShortcutService;

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

    [ObservableProperty]
    private bool _createDesktopShortcut;

    [ObservableProperty]
    private bool _isDesktopShortcutCreated;

    [ObservableProperty]
    private string _desktopShortcutStatusText = "Checking shortcut status...";

    [ObservableProperty]
    private string _desktopShortcutPath = string.Empty;

    [ObservableProperty]
    private string _targetExecutablePath = string.Empty;

    [ObservableProperty]
    private string _installationDirectoryPath = string.Empty;

    [ObservableProperty]
    private bool _isDevelopmentBuildWarning;

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
        KeyboardShortcutViewModel shortcutsViewModel,
        IDesktopShortcutService desktopShortcutService)
    {
        _settingsService = Guard.NotNull(settingsService);
        _pluginCatalogService = Guard.NotNull(pluginCatalogService);
        _notificationService = Guard.NotNull(notificationService);
        _pathsService = Guard.NotNull(pathsService);
        Shortcuts = Guard.NotNull(shortcutsViewModel);
        _desktopShortcutService = Guard.NotNull(desktopShortcutService);

        Title = "System Settings & Configuration";
        Subtitle = "Configure default export parameters, coordinate conventions, logging, format plugins, desktop shortcut, and keyboard shortcuts.";
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

            RefreshDesktopShortcutStatus();
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
                DecimalPrecision: DecimalPrecision,
                CreateDesktopShortcut: CreateDesktopShortcut);

            var result = await _settingsService.SavePreferencesAsync(dto, cancellationToken);
            if (result.IsFailure)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            if (CreateDesktopShortcut && !_desktopShortcutService.CheckShortcutExists())
            {
                _desktopShortcutService.EnsureShortcutOnStartup(true);
            }

            foreach (var plugin in RegisteredPlugins)
            {
                await _pluginCatalogService.SetPluginStateAsync(plugin.PluginId, plugin.IsEnabled, cancellationToken);
            }

            RefreshDesktopShortcutStatus();
            StatusMessage = "Preferences saved successfully.";
            _notificationService.Publish(
                "Settings Saved",
                "Application preferences, desktop shortcut settings, and export defaults have been updated.",
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
            RefreshDesktopShortcutStatus();
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

    [RelayCommand]
    public void RefreshDesktopShortcutStatus()
    {
        var status = _desktopShortcutService.GetShortcutStatus();
        IsDesktopShortcutCreated = status.Exists;
        DesktopShortcutPath = status.ShortcutPath;
        TargetExecutablePath = status.TargetPath;
        InstallationDirectoryPath = status.WorkingDirectory;
        IsDevelopmentBuildWarning = status.IsDevelopmentBuild;

        if (status.Exists)
        {
            DesktopShortcutStatusText = status.IsTargetValid
                ? "Active – Shortcut is present on Desktop and links to a valid executable."
                : "Broken Link – Shortcut exists but target executable was not found on disk.";
        }
        else
        {
            DesktopShortcutStatusText = "Not Created – No NEXUS-3D shortcut found on the Windows Desktop.";
        }
    }

    [RelayCommand]
    public void CreateDesktopShortcutNow()
    {
        var result = _desktopShortcutService.CreateShortcut(overwrite: false);
        RefreshDesktopShortcutStatus();

        if (result.IsSuccess)
        {
            _notificationService.Publish(
                "Shortcut Created",
                $"NEXUS-3D Desktop shortcut has been created successfully at:\n{DesktopShortcutPath}",
                NotificationSeverity.Success,
                "Shortcut");
        }
        else if (result.ErrorCode == "SHORTCUT_EXISTS")
        {
            _notificationService.Publish(
                "Shortcut Already Exists",
                "A desktop shortcut already exists. Click 'Recreate Shortcut' if you want to overwrite it.",
                NotificationSeverity.Info,
                "Shortcut");
        }
        else
        {
            _notificationService.Publish(
                "Shortcut Creation Failed",
                result.ErrorMessage,
                NotificationSeverity.Error,
                "Shortcut");
        }
    }

    [RelayCommand]
    public void RecreateDesktopShortcut()
    {
        var result = _desktopShortcutService.RecreateShortcut();
        RefreshDesktopShortcutStatus();

        if (result.IsSuccess)
        {
            _notificationService.Publish(
                "Shortcut Updated",
                "NEXUS-3D Desktop shortcut has been refreshed and updated successfully.",
                NotificationSeverity.Success,
                "Shortcut");
        }
        else
        {
            _notificationService.Publish(
                "Shortcut Update Failed",
                result.ErrorMessage,
                NotificationSeverity.Error,
                "Shortcut");
        }
    }

    [RelayCommand]
    public void RemoveDesktopShortcut()
    {
        var result = _desktopShortcutService.RemoveShortcut();
        RefreshDesktopShortcutStatus();

        if (result.IsSuccess)
        {
            _notificationService.Publish(
                "Shortcut Removed",
                "NEXUS-3D Desktop shortcut was removed from your Windows Desktop.",
                NotificationSeverity.Info,
                "Shortcut");
        }
        else
        {
            _notificationService.Publish(
                "Shortcut Removal Failed",
                result.ErrorMessage,
                NotificationSeverity.Error,
                "Shortcut");
        }
    }

    [RelayCommand]
    public void OpenInstallationFolder()
    {
        var result = _desktopShortcutService.OpenInstallationFolder();
        if (result.IsFailure)
        {
            _notificationService.Publish(
                "Failed to Open Folder",
                result.ErrorMessage,
                NotificationSeverity.Warning,
                "Shortcut");
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
        CreateDesktopShortcut = dto.CreateDesktopShortcut;
    }
}


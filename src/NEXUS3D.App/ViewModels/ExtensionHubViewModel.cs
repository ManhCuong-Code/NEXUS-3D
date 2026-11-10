using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.PluginSystem.Models;
using NEXUS3D.PluginSystem.Services;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Presentation wrapper for a loaded or discovered plugin item in the Extension Hub.
/// </summary>
public sealed partial class PluginItemViewModel : ObservableObject
{
    private readonly LoadedPluginInstance _instance;

    public LoadedPluginInstance Instance => _instance;
    public PluginManifest Manifest => _instance.Manifest;

    public string Id => Manifest.Id;
    public string Name => Manifest.Name;
    public string Version => Manifest.Version;
    public string Author => Manifest.Author;
    public string Description => Manifest.Description;
    public string ApiVersion => Manifest.PluginApiVersion;
    public string EntryPoint => Manifest.EntryPoint;
    public string RootDirectory => _instance.PluginDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled))]
    [NotifyPropertyChangedFor(nameof(IsDisabled))]
    [NotifyPropertyChangedFor(nameof(IsFaulted))]
    [NotifyPropertyChangedFor(nameof(StatusBadge))]
    [NotifyPropertyChangedFor(nameof(ToggleActionText))]
    private PluginLifecycleStatus _status;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsEnabled => Status == PluginLifecycleStatus.Enabled;
    public bool IsDisabled => Status == PluginLifecycleStatus.Disabled || Status == PluginLifecycleStatus.Loaded || Status == PluginLifecycleStatus.Initialized;
    public bool IsFaulted => Status == PluginLifecycleStatus.Faulted;

    public string StatusBadge => Status switch
    {
        PluginLifecycleStatus.Enabled => "ACTIVE",
        PluginLifecycleStatus.Disabled => "DISABLED",
        PluginLifecycleStatus.Faulted => "FAULTED",
        PluginLifecycleStatus.Loaded => "LOADED",
        PluginLifecycleStatus.Initialized => "INITIALIZED",
        PluginLifecycleStatus.Unloaded => "UNLOADED",
        _ => Status.ToString().ToUpperInvariant()
    };

    public string ToggleActionText => IsEnabled ? "Disable Plugin" : "Enable Plugin";

    public string FormatsDisplay => Manifest.SupportedFormats.Count > 0
        ? string.Join(", ", Manifest.SupportedFormats)
        : "No formats declared";

    public string CapabilitiesDisplay => Manifest.Capabilities.Count > 0
        ? string.Join(", ", Manifest.Capabilities)
        : "Standard extension";

    public string DependenciesDisplay => Manifest.Dependencies.Count > 0
        ? string.Join("; ", Manifest.Dependencies.Select(d => $"{d.PluginId} (>={d.MinimumVersion})"))
        : "None (Standalone)";

    public PluginItemViewModel(LoadedPluginInstance instance)
    {
        _instance = Guard.NotNull(instance);
        _status = instance.Status;
        _errorMessage = instance.FaultException?.Message;
    }

    public void UpdateStatus(PluginLifecycleStatus status, string? errorMessage = null)
    {
        Status = status;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// ViewModel for the NEXUS 3D Extension Hub workspace.
/// Coordinates browsing, inspection, installation, lifecycle management, and diagnostics of dynamically loaded plugins.
/// </summary>
public sealed partial class ExtensionHubViewModel : BaseViewModel
{
    private readonly IPluginManager _pluginManager;
    private readonly IApplicationPathsService _pathsService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private PluginItemViewModel? _selectedPlugin;

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private int _totalPluginCount;

    [ObservableProperty]
    private int _activePluginCount;

    [ObservableProperty]
    private int _disabledPluginCount;

    [ObservableProperty]
    private int _faultedPluginCount;

    public ObservableCollection<PluginItemViewModel> AllPlugins { get; } = [];
    public ObservableCollection<PluginItemViewModel> FilteredPlugins { get; } = [];

    public string PluginsDirectoryPath => _pathsService.PluginsDirectory;

    public ExtensionHubViewModel(
        IPluginManager pluginManager,
        IApplicationPathsService pathsService,
        INotificationService notificationService)
    {
        _pluginManager = Guard.NotNull(pluginManager);
        _pathsService = Guard.NotNull(pathsService);
        _notificationService = Guard.NotNull(notificationService);

        Title = "Plugin SDK & Extension Hub";
        Subtitle = "Manage dynamically loaded formats, custom parsers, exporters, and 3D extensions.";
    }

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken = default)
    {
        await RefreshPluginsAsync();
    }

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public async Task RefreshPluginsAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Scanning for extensions...";
            ClearError();

            await _pluginManager.DiscoverAndLoadPluginsAsync();

            var plugins = _pluginManager.GetAllPlugins();
            AllPlugins.Clear();

            foreach (var plugin in plugins)
            {
                AllPlugins.Add(new PluginItemViewModel(plugin));
            }

            UpdateTelemetry();
            ApplyFilter();

            if (SelectedPlugin != null)
            {
                SelectedPlugin = FilteredPlugins.FirstOrDefault(p => p.Id == SelectedPlugin.Id)
                                 ?? FilteredPlugins.FirstOrDefault();
            }
            else
            {
                SelectedPlugin = FilteredPlugins.FirstOrDefault();
            }

            StatusMessage = $"Extension Hub synchronized ({AllPlugins.Count} plugins registered).";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to scan plugins: {ex.Message}";
            _notificationService.Publish(
                "Plugin Discovery Failed",
                ex.Message,
                NotificationSeverity.Error,
                "ExtensionHub");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task InstallPackageAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Install NEXUS-3D Plugin Package",
            Filter = "NEXUS-3D Plugin (*.nexusplugin)|*.nexusplugin|Zip Archive (*.zip)|*.zip|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        string packagePath = openFileDialog.FileName;
        try
        {
            IsBusy = true;
            StatusMessage = $"Installing package '{Path.GetFileName(packagePath)}'...";

            var installResult = await _pluginManager.InstallPluginAsync(packagePath);
            if (installResult.IsFailure || installResult.Value == null)
            {
                ErrorMessage = installResult.ErrorMessage ?? "Plugin installation returned no instance.";
                _notificationService.Publish(
                    "Plugin Installation Failed",
                    ErrorMessage,
                    NotificationSeverity.Error,
                    "ExtensionHub");
                return;
            }

            _notificationService.Publish(
                "Plugin Installed Successfully",
                $"Plugin '{installResult.Value.Manifest.Name}' (v{installResult.Value.Manifest.Version}) installed and loaded.",
                NotificationSeverity.Success,
                "ExtensionHub");

            await RefreshPluginsAsync();

            SelectedPlugin = FilteredPlugins.FirstOrDefault(p => p.Id == installResult.Value.Manifest.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Installation error: {ex.Message}";
            _notificationService.Publish(
                "Plugin Installation Exception",
                ex.Message,
                NotificationSeverity.Error,
                "ExtensionHub");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task TogglePluginStatusAsync(PluginItemViewModel? target)
    {
        var item = target ?? SelectedPlugin;
        if (item == null) return;

        try
        {
            IsBusy = true;
            if (item.IsEnabled)
            {
                StatusMessage = $"Disabling '{item.Name}'...";
                var res = await _pluginManager.DisablePluginAsync(item.Id);
                if (res.IsFailure)
                {
                    ErrorMessage = res.ErrorMessage;
                    return;
                }
                item.UpdateStatus(PluginLifecycleStatus.Disabled);
                _notificationService.Publish(
                    "Plugin Disabled",
                    $"Plugin '{item.Name}' has been disabled.",
                    NotificationSeverity.Info,
                    "ExtensionHub");
            }
            else
            {
                StatusMessage = $"Enabling '{item.Name}'...";
                var res = await _pluginManager.EnablePluginAsync(item.Id);
                if (res.IsFailure)
                {
                    ErrorMessage = res.ErrorMessage;
                    item.UpdateStatus(PluginLifecycleStatus.Faulted, res.ErrorMessage);
                    return;
                }
                item.UpdateStatus(PluginLifecycleStatus.Enabled);
                _notificationService.Publish(
                    "Plugin Enabled",
                    $"Plugin '{item.Name}' is now active.",
                    NotificationSeverity.Success,
                    "ExtensionHub");
            }

            UpdateTelemetry();
            StatusMessage = $"Plugin '{item.Name}' state updated.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UninstallPluginAsync(PluginItemViewModel? target)
    {
        var item = target ?? SelectedPlugin;
        if (item == null) return;

        try
        {
            IsBusy = true;
            StatusMessage = $"Uninstalling '{item.Name}'...";

            var res = await _pluginManager.UninstallPluginAsync(item.Id);
            if (res.IsFailure)
            {
                ErrorMessage = res.ErrorMessage;
                return;
            }

            _notificationService.Publish(
                "Plugin Uninstalled",
                $"Plugin '{item.Name}' has been uninstalled.",
                NotificationSeverity.Warning,
                "ExtensionHub");

            await RefreshPluginsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ReloadPluginAsync(PluginItemViewModel? target)
    {
        var item = target ?? SelectedPlugin;
        if (item == null) return;

        try
        {
            IsBusy = true;
            StatusMessage = $"Reloading '{item.Name}'...";

            var res = await _pluginManager.ReloadPluginAsync(item.Id);
            if (res.IsFailure)
            {
                ErrorMessage = res.ErrorMessage;
                item.UpdateStatus(PluginLifecycleStatus.Faulted, res.ErrorMessage);
                return;
            }

            item.UpdateStatus(PluginLifecycleStatus.Enabled);
            _notificationService.Publish(
                "Plugin Reloaded",
                $"Plugin '{item.Name}' reloaded successfully.",
                NotificationSeverity.Success,
                "ExtensionHub");

            await RefreshPluginsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void OpenPluginsDirectory()
    {
        try
        {
            if (!Directory.Exists(PluginsDirectoryPath))
            {
                Directory.CreateDirectory(PluginsDirectoryPath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = PluginsDirectoryPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to open plugins folder: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        FilteredPlugins.Clear();
        var query = SearchFilter.Trim();

        foreach (var item in AllPlugins)
        {
            if (string.IsNullOrEmpty(query) ||
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.FormatsDisplay.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredPlugins.Add(item);
            }
        }
    }

    private void UpdateTelemetry()
    {
        TotalPluginCount = AllPlugins.Count;
        ActivePluginCount = AllPlugins.Count(p => p.IsEnabled);
        DisabledPluginCount = AllPlugins.Count(p => p.IsDisabled);
        FaultedPluginCount = AllPlugins.Count(p => p.IsFaulted);
    }
}

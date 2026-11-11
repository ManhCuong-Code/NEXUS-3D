using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NEXUS3D.App.Navigation;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel for the Dashboard overview screen.
/// Displays system metrics, supported 3D format adapters, recent files, and quick actions.
/// </summary>
public sealed partial class DashboardViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly IApplicationPathsService _pathsService;
    private readonly IFileManagerService _fileManagerService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private int _totalConversionsCount;

    [ObservableProperty]
    private int _recentFilesCount;

    [ObservableProperty]
    private int _conversionProfilesCount;

    [ObservableProperty]
    private int _supportedFormatsCount;

    [ObservableProperty]
    private int _activePluginsCount;

    [ObservableProperty]
    private bool _isDatabaseConnected;

    [ObservableProperty]
    private string _databasePathDisplay = string.Empty;

    [ObservableProperty]
    private string _logsPathDisplay = string.Empty;

    public ObservableCollection<IFormatDescriptor> SupportedFormats { get; } = [];
    public ObservableCollection<RecentFileEntry> RecentFiles { get; } = [];
    public ObservableCollection<ConversionHistoryRecord> RecentHistory { get; } = [];

    public DashboardViewModel(
        IDashboardService dashboardService,
        INavigationService navigationService,
        INotificationService notificationService,
        IApplicationPathsService pathsService,
        IFileManagerService fileManagerService,
        IServiceProvider serviceProvider)
    {
        _dashboardService = Guard.NotNull(dashboardService);
        _navigationService = Guard.NotNull(navigationService);
        _notificationService = Guard.NotNull(notificationService);
        _pathsService = Guard.NotNull(pathsService);
        _fileManagerService = Guard.NotNull(fileManagerService);
        _serviceProvider = Guard.NotNull(serviceProvider);

        Title = "Home Dashboard";
        Subtitle = "Universal 3D Model & Schematic Workspace Overview.";
    }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken = default) =>
        RefreshDashboardAsync(cancellationToken);

    [RelayCommand]
    public async Task RefreshDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();
            StatusMessage = "Loading workspace overview...";

            var result = await _dashboardService.GetDashboardSummaryAsync(cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                ErrorMessage = result.ErrorMessage;
                StatusMessage = "Failed to load dashboard metrics.";
                return;
            }

            var data = result.Value;
            TotalConversionsCount = data.TotalConversionsCount;
            RecentFilesCount = data.RecentFilesCount;
            ConversionProfilesCount = data.ConversionProfilesCount;
            SupportedFormatsCount = data.SupportedFormatsCount;
            ActivePluginsCount = data.ActivePluginsCount;
            IsDatabaseConnected = data.IsDatabaseConnected;
            DatabasePathDisplay = data.DatabaseFilePath;
            LogsPathDisplay = data.LogsDirectoryPath;

            SupportedFormats.Clear();
            foreach (var format in data.SupportedFormats)
            {
                SupportedFormats.Add(format);
            }

            RecentFiles.Clear();
            foreach (var file in data.RecentFiles)
            {
                RecentFiles.Add(file);
            }

            RecentHistory.Clear();
            foreach (var item in data.RecentHistory)
            {
                RecentHistory.Add(item);
            }

            StatusMessage = $"Workspace synchronized at {DateTime.Now:HH:mm:ss}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Quick Actions ---
    [RelayCommand]
    public async Task ImportModelAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import 3D Model or Schematic",
            Filter = "All Supported 3D Formats (*.obj;*.schem;*.schematic;*.geo.json;*.json;*.rawvox)|*.obj;*.schem;*.schematic;*.geo.json;*.json;*.rawvox|Wavefront OBJ (*.obj)|*.obj|Minecraft Schematics (*.schem;*.schematic)|*.schem;*.schematic|Bedrock Geometry (*.geo.json)|*.geo.json|Voxel JSON (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            await OpenFilePathAsync(dlg.FileName);
        }
    }

    [RelayCommand]
    public async Task NewProjectAsync()
    {
        await _navigationService.NavigateToAsync(NavigationModule.Viewport);
        var shell = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        await shell.NewProjectAsync();
    }

    [RelayCommand]
    public Task ConvertModelAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.Converter);

    [RelayCommand]
    public Task BatchConvertAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.BatchProcessing);

    [RelayCommand]
    public async Task OpenSchematicAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open Minecraft Schematic",
            Filter = "Minecraft Schematics (*.schem;*.schematic)|*.schem;*.schematic|Sponge v2/v3 (*.schem)|*.schem|Legacy MCEdit (*.schematic)|*.schematic"
        };

        if (dlg.ShowDialog() == true)
        {
            await OpenFilePathAsync(dlg.FileName);
        }
    }

    [RelayCommand]
    public async Task OpenRecentFileAsync(RecentFileEntry? entry)
    {
        if (entry is null || !File.Exists(entry.FilePath))
        {
            _notificationService.Publish("File Not Found", "The selected file no longer exists on disk.", NotificationSeverity.Warning, "RecentFiles");
            return;
        }

        await OpenFilePathAsync(entry.FilePath);
    }

    [RelayCommand]
    public async Task RemoveRecentFileAsync(RecentFileEntry? entry)
    {
        if (entry is null) return;

        // Removes from index only; DOES NOT delete from disk!
        var res = await _fileManagerService.RemoveRecentFileAsync(entry.Id);
        if (res.IsSuccess)
        {
            RecentFiles.Remove(entry);
            RecentFilesCount = RecentFiles.Count;
            _notificationService.Publish("Removed from Recents", $"Removed '{entry.FileName}' from recent list.", NotificationSeverity.Info, "RecentFiles");
        }
    }

    private async Task OpenFilePathAsync(string filePath)
    {
        var shell = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        await shell.LoadModelFileAsync(filePath);
    }

    [RelayCommand]
    public Task OpenFileManagerAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.FileManager);

    [RelayCommand]
    public Task OpenSettingsAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.Settings);

    [RelayCommand]
    public void OpenAppDataFolder()
    {
        try
        {
            _pathsService.EnsureDirectoriesExist();
            Process.Start(new ProcessStartInfo
            {
                FileName = _pathsService.AppDataRootDirectory,
                UseShellExecute = true
            });
            _notificationService.Publish(
                "Workspace Folder Opened",
                $"Opened {_pathsService.AppDataRootDirectory}",
                NotificationSeverity.Info,
                "Dashboard");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to open folder: {ex.Message}";
        }
    }
}

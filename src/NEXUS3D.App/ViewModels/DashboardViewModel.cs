using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        IApplicationPathsService pathsService)
    {
        _dashboardService = Guard.NotNull(dashboardService);
        _navigationService = Guard.NotNull(navigationService);
        _notificationService = Guard.NotNull(notificationService);
        _pathsService = Guard.NotNull(pathsService);

        Title = "Dashboard Overview";
        Subtitle = "Real-time workspace telemetry, active 3D format adapters, and quick operations.";
    }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken = default) =>
        RefreshDashboardAsync(cancellationToken);

    [RelayCommand]
    private async Task RefreshDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

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

    [RelayCommand]
    private Task OpenConverterWorkspaceAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.Converter);

    [RelayCommand]
    private Task OpenFileManagerAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.FileManager);

    [RelayCommand]
    private Task OpenSettingsAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.Settings);

    [RelayCommand]
    private void OpenAppDataFolder()
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


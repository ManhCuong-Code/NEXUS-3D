using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.App.Navigation;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Shared.Constants;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Shell ViewModel for <see cref="Views.MainWindow"/>.
/// Coordinates sidebar navigation, topbar status, status bar telemetry, and the slide-out Notification Panel.
/// </summary>
public sealed partial class MainWindowViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly IApplicationPathsService _pathsService;

    [ObservableProperty]
    private BaseViewModel? _currentViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardActive))]
    [NotifyPropertyChangedFor(nameof(IsConverterActive))]
    [NotifyPropertyChangedFor(nameof(IsViewportActive))]
    [NotifyPropertyChangedFor(nameof(IsVoxelizationActive))]
    [NotifyPropertyChangedFor(nameof(IsFileManagerActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    private NavigationModule _activeModule = NavigationModule.Dashboard;

    [ObservableProperty]
    private bool _isNotificationPanelOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadNotifications))]
    private int _unreadNotificationCount;

    [ObservableProperty]
    private string _latestNotificationText = "System Ready";

    [ObservableProperty]
    private string _databaseStatusText = "SQLite Connected";

    public string ApplicationTitle => AppConstants.ApplicationName;
    public string ApplicationSubtitle => AppConstants.ApplicationSubtitle;
    public string VersionBadge => $"v{AppConstants.Version} • {AppConstants.StageName}";
    public string WorkspacePathSummary => _pathsService.AppDataRootDirectory;

    public bool IsDashboardActive => ActiveModule == NavigationModule.Dashboard;
    public bool IsConverterActive => ActiveModule == NavigationModule.Converter;
    public bool IsViewportActive => ActiveModule == NavigationModule.Viewport;
    public bool IsVoxelizationActive => ActiveModule == NavigationModule.Voxelization;
    public bool IsFileManagerActive => ActiveModule == NavigationModule.FileManager;
    public bool IsSettingsActive => ActiveModule == NavigationModule.Settings;
    public bool HasUnreadNotifications => UnreadNotificationCount > 0;

    public ObservableCollection<NotificationMessage> Notifications { get; } = [];

    public MainWindowViewModel(
        INavigationService navigationService,
        INotificationService notificationService,
        IApplicationPathsService pathsService)
    {
        _navigationService = Guard.NotNull(navigationService);
        _notificationService = Guard.NotNull(notificationService);
        _pathsService = Guard.NotNull(pathsService);

        Title = $"{AppConstants.ApplicationName} – {AppConstants.ApplicationSubtitle}";
        StatusMessage = "Ready";

        _navigationService.Navigated += OnNavigated;
        _notificationService.NotificationPublished += OnNotificationPublished;

        SyncNotifications();
    }

    public async Task InitializeShellAsync(CancellationToken cancellationToken = default)
    {
        _notificationService.Publish(
            "NEXUS 3D Foundation Initialized",
            "Clean Architecture, DI, File Logging, and SQLite storage are online.",
            NotificationSeverity.Info,
            "Bootstrapper");

        await _navigationService.NavigateToAsync(NavigationModule.Dashboard, cancellationToken);
    }

    [RelayCommand]
    private Task NavigateDashboardAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.Dashboard);

    [RelayCommand]
    private Task NavigateConverterAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.Converter);

    [RelayCommand]
    private Task NavigateViewportAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.Viewport);

    [RelayCommand]
    private Task NavigateVoxelizationAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.Voxelization);

    [RelayCommand]
    private Task NavigateFileManagerAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.FileManager);

    [RelayCommand]
    private Task NavigateSettingsAsync() =>
        _navigationService.NavigateToAsync(NavigationModule.Settings);

    [RelayCommand]
    private void ToggleNotificationPanel()
    {
        IsNotificationPanelOpen = !IsNotificationPanelOpen;
        if (IsNotificationPanelOpen)
        {
            _notificationService.MarkAllAsRead();
            SyncNotifications();
        }
    }

    [RelayCommand]
    private void MarkAllNotificationsRead()
    {
        _notificationService.MarkAllAsRead();
        SyncNotifications();
    }

    [RelayCommand]
    private void ClearNotifications()
    {
        _notificationService.ClearAll();
        SyncNotifications();
        LatestNotificationText = "All notifications cleared.";
    }

    private void OnNavigated(object? sender, NavigationModule module)
    {
        ActiveModule = module;
        CurrentViewModel = _navigationService.CurrentViewModel;
        StatusMessage = $"Active Module: {module}";
    }

    private void OnNotificationPublished(object? sender, NotificationMessage e)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => HandleNewNotification(e));
        }
        else
        {
            HandleNewNotification(e);
        }
    }

    private void HandleNewNotification(NotificationMessage e)
    {
        LatestNotificationText = $"{e.Title}: {e.Message}";
        StatusMessage = LatestNotificationText;
        SyncNotifications();
    }

    private void SyncNotifications()
    {
        var all = _notificationService.GetNotifications();
        Notifications.Clear();
        foreach (var item in all)
        {
            Notifications.Add(item);
        }

        UnreadNotificationCount = all.Count(n => !n.IsRead);
    }
}


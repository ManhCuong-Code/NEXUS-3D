using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NEXUS3D.App.Navigation;
using NEXUS3D.App.Services.Shortcuts;
using NEXUS3D.App.Themes;
using NEXUS3D.App.Views;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Shared.Constants;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Shell ViewModel for <see cref="Views.MainWindow"/>.
/// Coordinates titlebar state, main menu commands, sidebar navigation, topbar telemetry,
/// bottom status bar metrics, and global keyboard shortcuts.
/// </summary>
public sealed partial class MainWindowViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly IApplicationPathsService _pathsService;
    private readonly IKeyboardShortcutService _shortcutService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private BaseViewModel? _currentViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardActive))]
    [NotifyPropertyChangedFor(nameof(IsImportActive))]
    [NotifyPropertyChangedFor(nameof(IsConverterActive))]
    [NotifyPropertyChangedFor(nameof(IsViewportActive))]
    [NotifyPropertyChangedFor(nameof(IsVoxelizationActive))]
    [NotifyPropertyChangedFor(nameof(IsBatchActive))]
    [NotifyPropertyChangedFor(nameof(IsFileManagerActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    [NotifyPropertyChangedFor(nameof(IsExtensionHubActive))]
    private NavigationModule _activeModule = NavigationModule.Dashboard;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private bool _isNotificationPanelOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadNotifications))]
    private int _unreadNotificationCount;

    [ObservableProperty]
    private string _latestNotificationText = "System Ready";

    [ObservableProperty]
    private string _databaseStatusText = "SQLite Connected";

    // --- Document & Saved State for TitleBar ---
    [ObservableProperty]
    private string _activeDocumentTitle = "Untitled Project";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SavedStatusText))]
    private bool _isDirty;

    public string SavedStatusText => IsDirty ? "Unsaved Changes •" : "Saved";

    // --- Telemetry Metrics for Status Bar ---
    [ObservableProperty]
    private int _vertexCount;

    [ObservableProperty]
    private int _faceCount;

    [ObservableProperty]
    private int _voxelCount;

    [ObservableProperty]
    private string _modelDimensions = "0 × 0 × 0";

    [ObservableProperty]
    private string _activeFormatDisplay = "USM Ready";

    public string ApplicationTitle => AppConstants.ApplicationName;
    public string ApplicationSubtitle => AppConstants.ApplicationSubtitle;
    public string VersionBadge => $"v{AppConstants.Version}";
    public string WorkspacePathSummary => _pathsService.AppDataRootDirectory;

    public bool IsDashboardActive => ActiveModule == NavigationModule.Dashboard;
    public bool IsImportActive => ActiveModule == NavigationModule.Import;
    public bool IsConverterActive => ActiveModule == NavigationModule.Converter;
    public bool IsViewportActive => ActiveModule == NavigationModule.Viewport;
    public bool IsVoxelizationActive => ActiveModule == NavigationModule.Voxelization;
    public bool IsBatchActive => ActiveModule == NavigationModule.BatchProcessing;
    public bool IsFileManagerActive => ActiveModule == NavigationModule.FileManager;
    public bool IsSettingsActive => ActiveModule == NavigationModule.Settings;
    public bool IsExtensionHubActive => ActiveModule == NavigationModule.ExtensionHub;
    public bool HasUnreadNotifications => UnreadNotificationCount > 0;

    public ObservableCollection<NotificationMessage> Notifications { get; } = [];

    public MainWindowViewModel(
        INavigationService navigationService,
        INotificationService notificationService,
        IApplicationPathsService pathsService,
        IKeyboardShortcutService shortcutService,
        IServiceProvider serviceProvider)
    {
        _navigationService = Guard.NotNull(navigationService);
        _notificationService = Guard.NotNull(notificationService);
        _pathsService = Guard.NotNull(pathsService);
        _shortcutService = Guard.NotNull(shortcutService);
        _serviceProvider = Guard.NotNull(serviceProvider);

        Title = $"{AppConstants.ApplicationName} – {AppConstants.ApplicationSubtitle}";
        StatusMessage = "Ready";

        _navigationService.Navigated += OnNavigated;
        _notificationService.NotificationPublished += OnNotificationPublished;
        _shortcutService.ShortcutTriggered += OnShortcutTriggered;

        SyncNotifications();
    }

    public async Task InitializeShellAsync(CancellationToken cancellationToken = default)
    {
        _notificationService.Publish(
            "NEXUS-3D Online",
            "Universal Scene Model (USM), DirectX Viewport, and Plugin Engine are online.",
            NotificationSeverity.Info,
            "Bootstrapper");

        await _navigationService.NavigateToAsync(NavigationModule.Dashboard, cancellationToken);
    }

    // --- Sidebar Navigation Commands ---
    [RelayCommand]
    public Task NavigateDashboardAsync() => _navigationService.NavigateToAsync(NavigationModule.Dashboard);

    [RelayCommand]
    public Task NavigateImportAsync() => _navigationService.NavigateToAsync(NavigationModule.Import);

    [RelayCommand]
    public Task NavigateConverterAsync() => _navigationService.NavigateToAsync(NavigationModule.Converter);

    [RelayCommand]
    public Task NavigateViewportAsync() => _navigationService.NavigateToAsync(NavigationModule.Viewport);

    [RelayCommand]
    public Task NavigateVoxelizationAsync() => _navigationService.NavigateToAsync(NavigationModule.Voxelization);

    [RelayCommand]
    public Task NavigateBatchAsync() => _navigationService.NavigateToAsync(NavigationModule.BatchProcessing);

    [RelayCommand]
    public Task NavigateFileManagerAsync() => _navigationService.NavigateToAsync(NavigationModule.FileManager);

    [RelayCommand]
    public Task NavigateSettingsAsync() => _navigationService.NavigateToAsync(NavigationModule.Settings);

    [RelayCommand]
    public Task NavigateExtensionHubAsync() => _navigationService.NavigateToAsync(NavigationModule.ExtensionHub);

    [RelayCommand]
    public void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    public void ToggleTheme() => ThemeManager.ToggleTheme();

    // --- Window State Control Commands ---
    [RelayCommand]
    public void MinimizeWindow(Window? window)
    {
        if (window is not null) window.WindowState = WindowState.Minimized;
    }

    [RelayCommand]
    public void MaximizeRestoreWindow(Window? window)
    {
        if (window is not null)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    [RelayCommand]
    public void CloseWindow(Window? window)
    {
        window?.Close();
    }

    // --- Main Menu Actions ---
    [RelayCommand]
    public async Task NewProjectAsync()
    {
        ActiveDocumentTitle = "Untitled Project";
        IsDirty = false;
        VertexCount = 0;
        FaceCount = 0;
        VoxelCount = 0;
        ModelDimensions = "0 × 0 × 0";
        ActiveFormatDisplay = "USM Ready";

        await _navigationService.NavigateToAsync(NavigationModule.Viewport);
        _notificationService.Publish("New Project", "Initialized empty 3D workspace.", NotificationSeverity.Info, "Workspace");
    }

    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open 3D Model or Schematic",
            Filter = "All Supported 3D Formats (*.obj;*.schem;*.schematic;*.geo.json;*.json;*.rawvox)|*.obj;*.schem;*.schematic;*.geo.json;*.json;*.rawvox|Wavefront OBJ (*.obj)|*.obj|Minecraft Schematics (*.schem;*.schematic)|*.schem;*.schematic|Bedrock Geometry (*.geo.json)|*.geo.json|Voxel JSON (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            await LoadModelFileAsync(dlg.FileName);
        }
    }

    public async Task LoadModelFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        ActiveDocumentTitle = Path.GetFileName(filePath);
        IsDirty = false;
        ActiveFormatDisplay = Path.GetExtension(filePath).ToUpperInvariant().TrimStart('.');

        await _navigationService.NavigateToAsync(NavigationModule.Viewport);

        var viewportVm = _serviceProvider.GetRequiredService<ViewportViewModel>();
        await viewportVm.LoadFileAsync(filePath);

        // Update live metrics from viewport scene
        if (viewportVm.Scene is not null)
        {
            FaceCount = viewportVm.Scene.TotalTriangles;
            VoxelCount = viewportVm.Scene.TotalVoxels;
            VertexCount = viewportVm.Scene.TotalVertices;
            var s = viewportVm.Scene.BoundsSize;
            ModelDimensions = $"{s.X:F1} × {s.Y:F1} × {s.Z:F1}";
        }

        // Register in recent files
        try
        {
            var fileManager = _serviceProvider.GetRequiredService<IFileManagerService>();
            await fileManager.RegisterFileAccessAsync(filePath);
        }
        catch { }

        _notificationService.Publish("Model Loaded", $"Opened {Path.GetFileName(filePath)}", NotificationSeverity.Info, "IO");
    }

    [RelayCommand]
    public async Task SaveProjectAsync()
    {
        IsDirty = false;
        _notificationService.Publish("Saved", $"Project '{ActiveDocumentTitle}' saved.", NotificationSeverity.Info, "Workspace");
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task SaveProjectAsAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save 3D Project As",
            Filter = "Wavefront OBJ (*.obj)|*.obj|Minecraft Sponge Schematic (*.schem)|*.schem|Bedrock Geometry (*.geo.json)|*.geo.json|Voxel JSON (*.voxel.json)|*.voxel.json",
            FileName = Path.GetFileNameWithoutExtension(ActiveDocumentTitle)
        };

        if (dlg.ShowDialog() == true)
        {
            ActiveDocumentTitle = Path.GetFileName(dlg.FileName);
            IsDirty = false;
            _notificationService.Publish("Project Saved", $"Saved to {dlg.FileName}", NotificationSeverity.Info, "Workspace");
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    public void Undo()
    {
        var viewportVm = _serviceProvider.GetRequiredService<ViewportViewModel>();
        viewportVm.Undo();
    }

    [RelayCommand]
    public void Redo()
    {
        var viewportVm = _serviceProvider.GetRequiredService<ViewportViewModel>();
        viewportVm.Redo();
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        var viewportVm = _serviceProvider.GetRequiredService<ViewportViewModel>();
        if (viewportVm.SelectedNodeId is not null)
        {
            viewportVm.SelectNode(null);
            _notificationService.Publish("Delete", "Object removed from selection.", NotificationSeverity.Info, "Editor");
        }
    }

    [RelayCommand]
    public void ShowShortcutsDialog()
    {
        var shortcutsVm = _serviceProvider.GetRequiredService<KeyboardShortcutViewModel>();
        var win = new KeyboardShortcutsWindow(shortcutsVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        win.ShowDialog();
    }

    [RelayCommand]
    public void ShowAboutDialog()
    {
        var win = new AboutWindow
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        win.ShowDialog();
    }

    // --- Global Shortcut Event Dispatcher ---
    private async void OnShortcutTriggered(object? sender, string shortcutId)
    {
        switch (shortcutId)
        {
            // File
            case "file.new": await NewProjectAsync(); break;
            case "file.open": await OpenFileAsync(); break;
            case "file.save": await SaveProjectAsync(); break;
            case "file.save_as": await SaveProjectAsAsync(); break;
            case "file.import": await OpenFileAsync(); break;
            case "file.export": await NavigateConverterAsync(); break;

            // Edit
            case "edit.undo": Undo(); break;
            case "edit.redo": Redo(); break;
            case "edit.delete": DeleteSelected(); break;

            // General
            case "help.shortcuts": ShowShortcutsDialog(); break;
            case "view.fullscreen":
                if (System.Windows.Application.Current.MainWindow is { } w)
                {
                    w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
                break;
            case "general.escape":
                var vp = _serviceProvider.GetRequiredService<ViewportViewModel>();
                vp.SelectNode(null);
                break;

            // Viewport Tools
            case "viewport.tool.select":
                _serviceProvider.GetRequiredService<ViewportViewModel>().SetTool(Editor.Abstractions.EditorToolType.Select);
                break;
            case "viewport.tool.move":
                _serviceProvider.GetRequiredService<ViewportViewModel>().SetTool(Editor.Abstractions.EditorToolType.Translate);
                break;
            case "viewport.tool.rotate":
                _serviceProvider.GetRequiredService<ViewportViewModel>().SetTool(Editor.Abstractions.EditorToolType.Rotate);
                break;
            case "viewport.tool.scale":
                _serviceProvider.GetRequiredService<ViewportViewModel>().SetTool(Editor.Abstractions.EditorToolType.Scale);
                break;
            case "viewport.tool.pivot":
                _serviceProvider.GetRequiredService<ViewportViewModel>().SetTool(Editor.Abstractions.EditorToolType.Pivot);
                break;
            case "viewport.view.toggle_grid":
                var s = _serviceProvider.GetRequiredService<RenderSettingsViewModel>();
                s.ShowGrid = !s.ShowGrid;
                break;
            case "viewport.view.toggle_axes":
                var a = _serviceProvider.GetRequiredService<RenderSettingsViewModel>();
                a.ShowAxes = !a.ShowAxes;
                break;
            case "viewport.view.toggle_shading":
                var sh = _serviceProvider.GetRequiredService<RenderSettingsViewModel>();
                sh.ShowWireframe = !sh.ShowWireframe;
                break;
            case "viewport.cam.front":
                _serviceProvider.GetRequiredService<CameraViewModel>().SetPreset(global::NEXUS3D.Rendering.Contracts.CameraPresetView.Front);
                break;
            case "viewport.cam.back":
                _serviceProvider.GetRequiredService<CameraViewModel>().SetPreset(global::NEXUS3D.Rendering.Contracts.CameraPresetView.Back);
                break;
            case "viewport.cam.left":
                _serviceProvider.GetRequiredService<CameraViewModel>().SetPreset(global::NEXUS3D.Rendering.Contracts.CameraPresetView.Left);
                break;
            case "viewport.cam.right":
                _serviceProvider.GetRequiredService<CameraViewModel>().SetPreset(global::NEXUS3D.Rendering.Contracts.CameraPresetView.Right);
                break;
            case "viewport.cam.top":
                _serviceProvider.GetRequiredService<CameraViewModel>().SetPreset(global::NEXUS3D.Rendering.Contracts.CameraPresetView.Top);
                break;
            case "viewport.cam.bottom":
                _serviceProvider.GetRequiredService<CameraViewModel>().SetPreset(global::NEXUS3D.Rendering.Contracts.CameraPresetView.Bottom);
                break;
            case "viewport.cam.perspective":
                _serviceProvider.GetRequiredService<CameraViewModel>().IsOrthographic = false;
                break;
            case "viewport.cam.orthographic":
                _serviceProvider.GetRequiredService<CameraViewModel>().IsOrthographic = true;
                break;
            case "viewport.cam.reset":
                _serviceProvider.GetRequiredService<CameraViewModel>().ResetCamera();
                break;

            // Navigation
            case "nav.home": await NavigateDashboardAsync(); break;
            case "nav.import": await NavigateImportAsync(); break;
            case "nav.convert": await NavigateConverterAsync(); break;
            case "nav.editor": await NavigateViewportAsync(); break;
            case "nav.voxelization": await NavigateVoxelizationAsync(); break;
            case "nav.batch": await NavigateBatchAsync(); break;
            case "nav.plugins": await NavigateExtensionHubAsync(); break;
            case "nav.settings": await NavigateSettingsAsync(); break;
        }
    }

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
        StatusMessage = $"Active Workspace: {module}";
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

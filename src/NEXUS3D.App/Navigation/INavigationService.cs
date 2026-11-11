using NEXUS3D.App.ViewModels;

namespace NEXUS3D.App.Navigation;

/// <summary>
/// Enumeration of primary navigation modules in NEXUS 3D.
/// </summary>
public enum NavigationModule
{
    Dashboard = 0,
    Converter = 1,
    FileManager = 2,
    Settings = 3,
    Viewport = 4,
    Voxelization = 5,
    ExtensionHub = 6,
    BatchProcessing = 7,
    Import = 8
}

/// <summary>
/// Service contract for switching between primary workspace views in the main shell.
/// </summary>
public interface INavigationService
{
    NavigationModule CurrentModule { get; }
    BaseViewModel? CurrentViewModel { get; }
    event EventHandler<NavigationModule>? Navigated;

    Task NavigateToAsync(NavigationModule module, CancellationToken cancellationToken = default);
}


using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.App.ViewModels;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.App.Navigation;

/// <summary>
/// Resolves module ViewModels from the DI container and manages active view state.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationModule CurrentModule { get; private set; } = NavigationModule.Dashboard;
    public BaseViewModel? CurrentViewModel { get; private set; }

    public event EventHandler<NavigationModule>? Navigated;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = Guard.NotNull(serviceProvider);
    }

    public async Task NavigateToAsync(NavigationModule module, CancellationToken cancellationToken = default)
    {
        BaseViewModel targetViewModel;

        switch (module)
        {
            case NavigationModule.Dashboard:
                targetViewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
                break;
            case NavigationModule.Converter:
                var convVm = _serviceProvider.GetRequiredService<ConverterViewModel>();
                convVm.SelectedWorkspaceTab = 0;
                targetViewModel = convVm;
                break;
            case NavigationModule.Import:
                var importVm = _serviceProvider.GetRequiredService<ConverterViewModel>();
                importVm.SelectedWorkspaceTab = 0;
                targetViewModel = importVm;
                break;
            case NavigationModule.BatchProcessing:
                var batchVm = _serviceProvider.GetRequiredService<ConverterViewModel>();
                batchVm.SelectedWorkspaceTab = 1;
                targetViewModel = batchVm;
                break;
            case NavigationModule.Viewport:
                targetViewModel = _serviceProvider.GetRequiredService<ViewportViewModel>();
                break;
            case NavigationModule.Voxelization:
                targetViewModel = _serviceProvider.GetRequiredService<VoxelizationViewModel>();
                break;
            case NavigationModule.FileManager:
                targetViewModel = _serviceProvider.GetRequiredService<FileManagerViewModel>();
                break;
            case NavigationModule.Settings:
                targetViewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
                break;
            case NavigationModule.ExtensionHub:
                targetViewModel = _serviceProvider.GetRequiredService<ExtensionHubViewModel>();
                break;
            default:
                targetViewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
                break;
        }

        CurrentModule = module;
        CurrentViewModel = targetViewModel;
        Navigated?.Invoke(this, module);

        await targetViewModel.OnNavigatedToAsync(cancellationToken);
    }
}


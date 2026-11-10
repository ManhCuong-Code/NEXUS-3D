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
        BaseViewModel targetViewModel = module switch
        {
            NavigationModule.Dashboard => _serviceProvider.GetRequiredService<DashboardViewModel>(),
            NavigationModule.Converter => _serviceProvider.GetRequiredService<ConverterViewModel>(),
            NavigationModule.Viewport => _serviceProvider.GetRequiredService<ViewportViewModel>(),
            NavigationModule.Voxelization => _serviceProvider.GetRequiredService<VoxelizationViewModel>(),
            NavigationModule.FileManager => _serviceProvider.GetRequiredService<FileManagerViewModel>(),
            NavigationModule.Settings => _serviceProvider.GetRequiredService<SettingsViewModel>(),
            _ => _serviceProvider.GetRequiredService<DashboardViewModel>()
        };

        CurrentModule = module;
        CurrentViewModel = targetViewModel;
        Navigated?.Invoke(this, module);

        await targetViewModel.OnNavigatedToAsync(cancellationToken);
    }
}


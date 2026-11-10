using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS3D.App.ViewModels;
using NEXUS3D.App.Views;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Infrastructure.Logging;

namespace NEXUS3D.App.Bootstrap;

/// <summary>
/// Bootstraps the NEXUS 3D WPF application: builds the DI container, configures global exception hooks,
/// initializes the SQLite database, and launches the main shell window.
/// </summary>
public sealed class AppBootstrapper : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private bool _disposed;

    public IServiceProvider ServiceProvider =>
        _serviceProvider ?? throw new InvalidOperationException("Bootstrapper has not been initialized.");

    public async Task InitializeAndStartAsync(System.Windows.Application wpfApp)
    {
        _serviceProvider = ServiceRegistration.BuildNexusServiceProvider();

        RegisterGlobalExceptionHandlers(wpfApp);

        var logger = _serviceProvider.GetRequiredService<ILogger<AppBootstrapper>>();
        logger.LogInformation("Starting NEXUS 3D Foundation bootstrapper...");

        var dbInitializer = _serviceProvider.GetRequiredService<IDatabaseInitializer>();
        var initResult = await dbInitializer.InitializeAsync();
        if (initResult.IsFailure)
        {
            var notificationService = _serviceProvider.GetRequiredService<INotificationService>();
            notificationService.Publish(
                "Database Initialization Warning",
                initResult.ErrorMessage,
                NotificationSeverity.Warning,
                "SQLite");
        }

        try
        {
            var pluginManager = _serviceProvider.GetRequiredService<NEXUS3D.PluginSystem.Services.IPluginManager>();
            await pluginManager.DiscoverAndLoadPluginsAsync();
            logger.LogInformation("NEXUS-3D Plugin System initialized: {Count} active format extensions loaded.",
                pluginManager.GetActiveFormatDescriptors().Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize some plugins during startup.");
        }

        var shellViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = shellViewModel;
        wpfApp.MainWindow = mainWindow;
        mainWindow.Show();

        await shellViewModel.InitializeShellAsync();
        logger.LogInformation("NEXUS 3D main window initialized and displayed.");
    }

    private void RegisterGlobalExceptionHandlers(System.Windows.Application wpfApp)
    {
        wpfApp.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var exceptionLogger = _serviceProvider?.GetService<ExceptionLogger>();
            exceptionLogger?.LogUnhandledException(e.Exception, "WPF.DispatcherUnhandledException", isTerminating: false);

            var notificationService = _serviceProvider?.GetService<INotificationService>();
            notificationService?.Publish(
                "Unhandled UI Error",
                e.Exception.Message,
                NotificationSeverity.Error,
                "UI.Dispatcher");
        }
        finally
        {
            // Prevent unexpected application crash on recoverable UI exceptions after logging.
            e.Handled = true;
        }
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            var exceptionLogger = _serviceProvider?.GetService<ExceptionLogger>();
            exceptionLogger?.LogUnhandledException(ex, "AppDomain.UnhandledException", e.IsTerminating);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var exceptionLogger = _serviceProvider?.GetService<ExceptionLogger>();
        exceptionLogger?.LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException", isTerminating: false);
        e.SetObserved();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _serviceProvider?.Dispose();
    }
}


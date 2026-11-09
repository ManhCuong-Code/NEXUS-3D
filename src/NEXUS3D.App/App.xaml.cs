using System.Windows;
using NEXUS3D.App.Bootstrap;

namespace NEXUS3D.App;

/// <summary>
/// Application entry point. Delegates startup, DI registration, logging, and SQLite initialization to <see cref="AppBootstrapper"/>.
/// </summary>
public partial class App : System.Windows.Application
{
    private AppBootstrapper? _bootstrapper;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _bootstrapper = new AppBootstrapper();
        await _bootstrapper.InitializeAndStartAsync(this);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bootstrapper?.Dispose();
        base.OnExit(e);
    }
}

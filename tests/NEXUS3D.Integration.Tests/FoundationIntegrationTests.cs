using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS3D.App.Bootstrap;
using NEXUS3D.App.Navigation;
using NEXUS3D.App.ViewModels;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Infrastructure.Logging;

namespace NEXUS3D.Integration.Tests;

public sealed class FoundationIntegrationTests : IDisposable
{
    private readonly string _tempRootDir;
    private readonly ServiceProvider _serviceProvider;

    public FoundationIntegrationTests()
    {
        _tempRootDir = Path.Combine(Path.GetTempPath(), $"NEXUS3D_Tests_{Guid.NewGuid():N}");
        _serviceProvider = ServiceRegistration.BuildNexusServiceProvider(new DependencyOptions
        {
            CustomAppDataRoot = _tempRootDir,
            MinimumLogLevel = LogLevel.Information
        });
    }

    [Fact]
    public async Task DatabaseInitializer_ShouldMigrateSchemaAndSeedDefaultRecords()
    {
        var dbInitializer = _serviceProvider.GetRequiredService<IDatabaseInitializer>();
        var initResult = await dbInitializer.InitializeAsync();

        Assert.True(initResult.IsSuccess);
        Assert.True(await dbInitializer.CheckHealthAsync());

        var dashboardService = _serviceProvider.GetRequiredService<IDashboardService>();
        var summaryResult = await dashboardService.GetDashboardSummaryAsync();

        Assert.True(summaryResult.IsSuccess);
        Assert.NotNull(summaryResult.Value);
        Assert.True(summaryResult.Value.IsDatabaseConnected);
        Assert.Equal(4, summaryResult.Value.SupportedFormatsCount);
        Assert.True(summaryResult.Value.ConversionProfilesCount >= 4);
        Assert.True(summaryResult.Value.ActivePluginsCount >= 4);
    }

    [Fact]
    public void FileLoggingAndExceptionLogger_ShouldWriteToFileAndRedactSecrets()
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<FoundationIntegrationTests>>();
        var exceptionLogger = _serviceProvider.GetRequiredService<ExceptionLogger>();
        var logConfig = _serviceProvider.GetRequiredService<LogConfiguration>();

        logger.LogInformation("Integration test started with api_key=SUPER_SECRET_VALUE_999");

        try
        {
            throw new InvalidOperationException("Simulated geometry pipeline error.");
        }
        catch (Exception ex)
        {
            exceptionLogger.LogHandledException(ex, "IntegrationTestComponent", "Validating stack trace logging");
        }

        string logFilePath = logConfig.GetCurrentLogFilePath(DateTimeOffset.UtcNow);
        Assert.True(File.Exists(logFilePath));

        string logContents = File.ReadAllText(logFilePath);
        Assert.Contains("Integration test started", logContents);
        Assert.DoesNotContain("SUPER_SECRET_VALUE_999", logContents);
        Assert.Contains("api_key=***REDACTED***", logContents);
        Assert.Contains("Simulated geometry pipeline error.", logContents);
        Assert.Contains("StackTrace:", logContents);
    }

    [Fact]
    public async Task MvvmNavigationAndConverterPreparation_ShouldPersistJobAndRecentFile()
    {
        var dbInitializer = _serviceProvider.GetRequiredService<IDatabaseInitializer>();
        await dbInitializer.InitializeAsync();

        string sampleFile = Path.Combine(_tempRootDir, "sample_castle.schem");
        await File.WriteAllBytesAsync(sampleFile, [0x01, 0x02, 0x03, 0x04]);

        var converterService = _serviceProvider.GetRequiredService<IConverterPreparationService>();
        var prepResult = await converterService.PrepareConversionJobAsync(new PrepareConversionRequest(
            SourceFilePath: sampleFile,
            OutputDirectory: Path.Combine(_tempRootDir, "Exports"),
            SourceFormat: ModelFormatType.Schematic,
            TargetFormat: ModelFormatType.Obj,
            ProfileId: null,
            ScaleFactor: 1.0,
            OptimizeMesh: true,
            GreedyMeshing: true,
            PreserveMaterials: true));

        Assert.True(prepResult.IsSuccess);
        Assert.NotNull(prepResult.Value);

        var shellVm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        await shellVm.InitializeShellAsync();
        Assert.Equal(NavigationModule.Dashboard, shellVm.ActiveModule);
        Assert.IsType<DashboardViewModel>(shellVm.CurrentViewModel);

        var navService = _serviceProvider.GetRequiredService<INavigationService>();
        await navService.NavigateToAsync(NavigationModule.FileManager);
        Assert.Equal(NavigationModule.FileManager, shellVm.ActiveModule);

        var fileManagerVm = _serviceProvider.GetRequiredService<FileManagerViewModel>();
        Assert.Single(fileManagerVm.RecentFiles);
        Assert.Single(fileManagerVm.ConversionHistory);
    }

    [Fact]
    public void WpfViewsAndResourceDictionaries_ShouldInstantiateWithoutXamlErrorsOnStaThread()
    {
        Exception? staException = null;

        var staThread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var app = new NEXUS3D.App.App();
                    app.InitializeComponent();
                }

                var shellVm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                var dashboardView = new NEXUS3D.App.Views.DashboardView
                {
                    DataContext = _serviceProvider.GetRequiredService<DashboardViewModel>()
                };
                var converterView = new NEXUS3D.App.Views.ConverterView
                {
                    DataContext = _serviceProvider.GetRequiredService<ConverterViewModel>()
                };
                var fileManagerView = new NEXUS3D.App.Views.FileManagerView
                {
                    DataContext = _serviceProvider.GetRequiredService<FileManagerViewModel>()
                };
                var settingsView = new NEXUS3D.App.Views.SettingsView
                {
                    DataContext = _serviceProvider.GetRequiredService<SettingsViewModel>()
                };
                var mainWindow = new NEXUS3D.App.Views.MainWindow
                {
                    DataContext = shellVm
                };

                Assert.NotNull(dashboardView);
                Assert.NotNull(converterView);
                Assert.NotNull(fileManagerView);
                Assert.NotNull(settingsView);
                Assert.NotNull(mainWindow);
            }
            catch (Exception ex)
            {
                staException = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        Assert.Null(staException);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        try
        {
            if (Directory.Exists(_tempRootDir))
            {
                Directory.Delete(_tempRootDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup locks on SQLite pool handles in temp folder.
        }
    }
}

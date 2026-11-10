using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Infrastructure.Configuration;
using NEXUS3D.Infrastructure.Formats;
using NEXUS3D.Infrastructure.Persistence;
using NEXUS3D.Infrastructure.Persistence.Repositories;
using NEXUS3D.PluginSystem.Models;
using NEXUS3D.PluginSystem.Services;
using Xunit;

namespace NEXUS3D.Plugin.Integration.Tests;

public sealed class PluginLifecycleIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ServiceProvider _serviceProvider;
    private readonly IFormatRegistry _formatRegistry;
    private readonly IPluginManager _pluginManager;

    public PluginLifecycleIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "Nexus3D_PluginLifeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        string dbPath = Path.Combine(_tempDirectory, "nexus3d_test.db");
        string pluginsDir = Path.Combine(_tempDirectory, "plugins");
        Directory.CreateDirectory(pluginsDir);

        // Copy sample plugin build output to pluginsDir
        string samplePluginSourceDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../plugins/NEXUS3D.Sample.Plugin"));
        string sampleTargetDir = Path.Combine(pluginsDir, "NEXUS3D.Sample.Plugin");
        Directory.CreateDirectory(sampleTargetDir);

        // Copy manifest and binaries
        string manifestSource = Path.Combine(samplePluginSourceDir, "manifest.json");
        if (File.Exists(manifestSource))
        {
            File.Copy(manifestSource, Path.Combine(sampleTargetDir, "manifest.json"), true);
        }

        string binReleaseDir = Path.Combine(samplePluginSourceDir, "bin", "Release", "net10.0");
        string binDebugDir = Path.Combine(samplePluginSourceDir, "bin", "Debug", "net10.0");
        string binDir = Directory.Exists(binReleaseDir) ? binReleaseDir : binDebugDir;

        if (Directory.Exists(binDir))
        {
            foreach (var file in Directory.GetFiles(binDir, "*.dll"))
            {
                File.Copy(file, Path.Combine(sampleTargetDir, Path.GetFileName(file)), true);
            }
        }

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton(new DatabaseConfiguration(dbPath));
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseSchemaMigrator>();
        services.AddSingleton<IPluginRegistrationRepository, SqlitePluginRegistrationRepository>();
        services.AddSingleton<IFormatRegistry, FormatRegistry>();

        services.AddSingleton<IPluginManager>(sp =>
        {
            var repo = sp.GetRequiredService<IPluginRegistrationRepository>();
            var reg = sp.GetRequiredService<IFormatRegistry>();
            var logger = sp.GetRequiredService<ILogger<PluginManager>>();
            return new PluginManager(repo, reg, sp, logger, pluginsDir, Path.Combine(_tempDirectory, "data"));
        });

        _serviceProvider = services.BuildServiceProvider();

        // Run migrations
        var factory = _serviceProvider.GetRequiredService<SqliteConnectionFactory>();
        var migrator = _serviceProvider.GetRequiredService<DatabaseSchemaMigrator>();
        using var connection = factory.CreateOpenConnectionAsync().GetAwaiter().GetResult();
        migrator.MigrateAsync(connection).GetAwaiter().GetResult();

        _formatRegistry = _serviceProvider.GetRequiredService<IFormatRegistry>();
        _pluginManager = _serviceProvider.GetRequiredService<IPluginManager>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public async Task DiscoverAndLoadPluginsAsync_LoadsRawVoxPlugin_RegistersInRegistry()
    {
        // 1. Discover and load plugins
        await _pluginManager.DiscoverAndLoadPluginsAsync();

        var plugins = _pluginManager.GetAllPlugins();
        Assert.NotEmpty(plugins);

        var rawVoxPlugin = plugins.FirstOrDefault(p => p.Manifest.Id == "com.nexus3d.formats.rawvox");
        Assert.NotNull(rawVoxPlugin);
        Assert.Equal(PluginLifecycleStatus.Enabled, rawVoxPlugin.Status);

        // 2. Verify format descriptor registered in IFormatRegistry
        var descriptor = _formatRegistry.ResolveByFilePath("test.rawvox");
        Assert.NotNull(descriptor);
        Assert.Contains("RawVox", descriptor.DisplayName);

        // 3. Test DisablePluginAsync
        var disableResult = await _pluginManager.DisablePluginAsync("com.nexus3d.formats.rawvox");
        Assert.True(disableResult.IsSuccess);
        Assert.Equal(PluginLifecycleStatus.Disabled, rawVoxPlugin.Status);

        // Verify descriptor removed from IFormatRegistry
        var descAfterDisable = _formatRegistry.ResolveByFilePath("test.rawvox");
        Assert.Null(descAfterDisable);

        // 4. Test EnablePluginAsync
        var enableResult = await _pluginManager.EnablePluginAsync("com.nexus3d.formats.rawvox");
        Assert.True(enableResult.IsSuccess);
        Assert.Equal(PluginLifecycleStatus.Enabled, rawVoxPlugin.Status);

        // Verify descriptor restored
        var descAfterEnable = _formatRegistry.ResolveByFilePath("test.rawvox");
        Assert.NotNull(descAfterEnable);
    }
}

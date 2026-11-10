using NEXUS3D.Formats.Abstractions;
using NEXUS3D.PluginSystem.Models;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.PluginSystem.Services;

/// <summary>
/// Central manager for discovering, loading, installing, configuring, and executing plugins.
/// </summary>
public interface IPluginManager : IDynamicFormatProvider
{
    /// <summary>
    /// Scans discovered plugin directories, validates manifests, resolves dependencies, and initializes active plugins.
    /// </summary>
    Task DiscoverAndLoadPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a new `.nexusplugin` archive package safely, validates its manifest, and loads it.
    /// </summary>
    Task<OperationResult<LoadedPluginInstance>> InstallPluginAsync(string packageFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables an installed plugin, activating its format contributions.
    /// </summary>
    Task<OperationResult> EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables an installed plugin and deactivates its format contributions.
    /// </summary>
    Task<OperationResult> DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely shuts down and uninstalls a plugin, removing its files from disk.
    /// </summary>
    Task<OperationResult> UninstallPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shuts down, unloads, and reloads a specific plugin.
    /// </summary>
    Task<OperationResult> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all discovered or installed plugins.
    /// </summary>
    IReadOnlyList<LoadedPluginInstance> GetAllPlugins();

    /// <summary>
    /// Returns a specific loaded plugin by its unique identifier.
    /// </summary>
    LoadedPluginInstance? GetPlugin(string pluginId);

    /// <summary>
    /// Returns all format descriptors contributed by currently enabled plugins.
    /// </summary>
    IReadOnlyList<IFormatDescriptor> GetActiveFormatDescriptors();

    /// <summary>
    /// Returns all format parsers contributed by currently enabled plugins.
    /// </summary>
    IReadOnlyList<IFormatParser> GetActiveParsers();

    /// <summary>
    /// Returns all format exporters contributed by currently enabled plugins.
    /// </summary>
    IReadOnlyList<IFormatExporter> GetActiveExporters();

    /// <summary>
    /// Returns all format detectors contributed by currently enabled plugins.
    /// </summary>
    IReadOnlyList<IFormatDetector> GetActiveDetectors();

    /// <summary>
    /// Returns all format validators contributed by currently enabled plugins.
    /// </summary>
    IReadOnlyList<IFormatValidator> GetActiveValidators();
}

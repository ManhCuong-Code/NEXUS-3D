namespace NEXUS3D.Plugin.SDK.Contracts;

/// <summary>
/// Execution environment passed to a plugin during its lifecycle.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Service provider for querying host capabilities.
    /// </summary>
    IServiceProvider HostServices { get; }

    /// <summary>
    /// Isolated logger scoped to this plugin ID.
    /// </summary>
    IPluginLogger Logger { get; }

    /// <summary>
    /// Isolated configuration store for this plugin.
    /// </summary>
    IPluginConfiguration Configuration { get; }

    /// <summary>
    /// Registration context for contributing parsers, exporters, detectors, and descriptors.
    /// </summary>
    IPluginRegistrationContext Registration { get; }

    /// <summary>
    /// Root directory containing the plugin assembly and package assets.
    /// </summary>
    string PluginDirectory { get; }

    /// <summary>
    /// Writable directory for plugin data storage, logs, and caches.
    /// </summary>
    string DataDirectory { get; }
}


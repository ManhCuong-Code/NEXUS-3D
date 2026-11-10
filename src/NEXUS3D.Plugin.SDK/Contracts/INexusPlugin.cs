using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Plugin.SDK.Contracts;

/// <summary>
/// Root contract implemented by external NEXUS-3D plugins.
/// Managed dynamically by the Plugin Host Runtime.
/// </summary>
public interface INexusPlugin
{
    /// <summary>
    /// Metadata manifest describing this plugin's identity, version, author, and capabilities.
    /// </summary>
    PluginManifest Manifest { get; }

    /// <summary>
    /// Invoked when the plugin is loaded and initialized by the host runtime.
    /// </summary>
    Task<OperationResult> InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked when the plugin is being disabled or unloaded to clean up resources and cancel tasks.
    /// </summary>
    Task<OperationResult> ShutdownAsync(
        CancellationToken cancellationToken = default);
}

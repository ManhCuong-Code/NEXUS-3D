using NEXUS3D.Domain.Entities;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.PluginSystem.Contracts;

/// <summary>
/// Contract implemented by external NEXUS 3D plugins.
/// </summary>
public interface INexusPlugin
{
    string PluginId { get; }
    string Name { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }

    Task<OperationResult> InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Contract for discovering and managing plugin registrations in the foundation layer.
/// </summary>
public interface IPluginCatalogService
{
    Task<IReadOnlyList<PluginRegistration>> GetRegisteredPluginsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> SetPluginStateAsync(string pluginId, bool isEnabled, CancellationToken cancellationToken = default);
}


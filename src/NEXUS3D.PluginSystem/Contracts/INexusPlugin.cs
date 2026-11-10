using NEXUS3D.Domain.Entities;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.PluginSystem.Contracts;

/// <summary>
/// Alias forwarding to the core SDK plugin contract.
/// </summary>
public interface INexusPlugin : NEXUS3D.Plugin.SDK.Contracts.INexusPlugin
{
}

/// <summary>
/// Contract for discovering and managing plugin registrations in the foundation layer.
/// </summary>
public interface IPluginCatalogService
{
    Task<IReadOnlyList<PluginRegistration>> GetRegisteredPluginsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> SetPluginStateAsync(string pluginId, bool isEnabled, CancellationToken cancellationToken = default);
}

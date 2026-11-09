using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.PluginSystem.Contracts;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.PluginSystem.Services;

/// <summary>
/// Manages plugin registration metadata backed by the domain repository.
/// </summary>
public sealed class PluginCatalogService : IPluginCatalogService
{
    private readonly IPluginRegistrationRepository _pluginRepository;

    public PluginCatalogService(IPluginRegistrationRepository pluginRepository)
    {
        _pluginRepository = Guard.NotNull(pluginRepository);
    }

    public Task<IReadOnlyList<PluginRegistration>> GetRegisteredPluginsAsync(CancellationToken cancellationToken = default) =>
        _pluginRepository.GetAllAsync(cancellationToken);

    public async Task<OperationResult> SetPluginStateAsync(
        string pluginId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return OperationResult.Failure("PLUGIN_ID_EMPTY", "Plugin identifier is required.");
        }

        await _pluginRepository.SetEnabledAsync(pluginId, isEnabled, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success();
    }
}


using NEXUS3D.Plugin.SDK.Contracts;
using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Plugin.SDK.Base;

/// <summary>
/// Convenient abstract base class providing common lifecycle management, logging, and registration helper methods.
/// </summary>
public abstract class NexusPluginBase : INexusPlugin
{
    public PluginManifest Manifest { get; protected set; }

    protected IPluginContext? Context { get; private set; }
    protected IPluginLogger? Logger => Context?.Logger;
    protected IPluginConfiguration? Configuration => Context?.Configuration;
    protected IPluginRegistrationContext? Registration => Context?.Registration;

    protected NexusPluginBase(PluginManifest manifest)
    {
        Manifest = Guard.NotNull(manifest);
    }

    public async Task<OperationResult> InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken = default)
    {
        Context = Guard.NotNull(context);
        Logger?.LogInformation($"Initializing plugin '{Manifest.Name}' (v{Manifest.Version})...");

        try
        {
            var result = await OnInitializeAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                Logger?.LogInformation($"Plugin '{Manifest.Name}' initialized successfully.");
            }
            else
            {
                Logger?.LogError($"Plugin '{Manifest.Name}' initialization failed: {result.ErrorMessage}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Exception while initializing plugin '{Manifest.Name}'.", ex);
            return OperationResult.Failure("PLUGIN_INIT_EXCEPTION", ex.Message);
        }
    }

    public async Task<OperationResult> ShutdownAsync(
        CancellationToken cancellationToken = default)
    {
        Logger?.LogInformation($"Shutting down plugin '{Manifest.Name}'...");

        try
        {
            var result = await OnShutdownAsync(cancellationToken).ConfigureAwait(false);
            Logger?.LogInformation($"Plugin '{Manifest.Name}' shutdown completed.");
            return result;
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Exception during shutdown of plugin '{Manifest.Name}'.", ex);
            return OperationResult.Failure("PLUGIN_SHUTDOWN_EXCEPTION", ex.Message);
        }
    }

    /// <summary>
    /// Override to register descriptors, parsers, exporters, detectors, and validators.
    /// </summary>
    protected virtual Task<OperationResult> OnInitializeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult.Success());

    /// <summary>
    /// Override to perform cleanup when the plugin is disabled or unloaded.
    /// </summary>
    protected virtual Task<OperationResult> OnShutdownAsync(CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult.Success());
}

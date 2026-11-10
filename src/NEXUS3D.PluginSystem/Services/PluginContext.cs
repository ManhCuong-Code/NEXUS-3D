using NEXUS3D.Plugin.SDK.Contracts;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.PluginSystem.Services;

/// <summary>
/// Concrete execution context provided to an active plugin during its lifecycle.
/// </summary>
public sealed class PluginContext : IPluginContext
{
    public IServiceProvider HostServices { get; }
    public IPluginLogger Logger { get; }
    public IPluginConfiguration Configuration { get; }
    public IPluginRegistrationContext Registration { get; }
    public string PluginDirectory { get; }
    public string DataDirectory { get; }

    public PluginContext(
        IServiceProvider hostServices,
        IPluginLogger logger,
        IPluginConfiguration configuration,
        IPluginRegistrationContext registration,
        string pluginDirectory,
        string dataDirectory)
    {
        HostServices = Guard.NotNull(hostServices);
        Logger = Guard.NotNull(logger);
        Configuration = Guard.NotNull(configuration);
        Registration = Guard.NotNull(registration);
        PluginDirectory = Guard.NotNullOrWhiteSpace(pluginDirectory);
        DataDirectory = Guard.NotNullOrWhiteSpace(dataDirectory);
    }
}


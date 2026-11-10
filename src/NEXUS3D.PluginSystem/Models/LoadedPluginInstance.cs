using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Plugin.SDK.Contracts;
using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.PluginSystem.Loading;

namespace NEXUS3D.PluginSystem.Models;

/// <summary>
/// Runtime representation of a dynamically loaded plugin within the host process.
/// </summary>
public sealed class LoadedPluginInstance
{
    public PluginManifest Manifest { get; }
    public INexusPlugin? Plugin { get; internal set; }
    public PluginLoadContext? LoadContext { get; internal set; }
    public IPluginContext? Context { get; internal set; }
    public PluginLifecycleStatus Status { get; internal set; }
    public string? FaultMessage { get; internal set; }
    public Exception? FaultException { get; internal set; }
    public string AssemblyPath { get; }
    public string PluginDirectory { get; }
    public DateTimeOffset LoadedAtUtc { get; } = DateTimeOffset.UtcNow;

    public List<IFormatDescriptor> RegisteredDescriptors { get; } = [];
    public List<IFormatParser> RegisteredParsers { get; } = [];
    public List<IFormatExporter> RegisteredExporters { get; } = [];
    public List<IFormatDetector> RegisteredDetectors { get; } = [];
    public List<IFormatValidator> RegisteredValidators { get; } = [];

    public LoadedPluginInstance(
        PluginManifest manifest,
        string assemblyPath,
        string pluginDirectory,
        PluginLifecycleStatus initialStatus = PluginLifecycleStatus.Loaded)
    {
        Manifest = manifest;
        AssemblyPath = assemblyPath;
        PluginDirectory = pluginDirectory;
        Status = initialStatus;
    }
}


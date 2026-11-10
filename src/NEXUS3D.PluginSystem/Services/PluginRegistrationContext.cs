using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Plugin.SDK.Contracts;
using NEXUS3D.PluginSystem.Models;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.PluginSystem.Services;

/// <summary>
/// Collects format descriptors, parsers, exporters, detectors, and validators registered by a plugin instance.
/// </summary>
public sealed class PluginRegistrationContext : IPluginRegistrationContext
{
    private readonly LoadedPluginInstance _instance;

    public PluginRegistrationContext(LoadedPluginInstance instance)
    {
        _instance = Guard.NotNull(instance);
    }

    public void RegisterDescriptor(IFormatDescriptor descriptor)
    {
        Guard.NotNull(descriptor);
        _instance.RegisteredDescriptors.Add(descriptor);
    }

    public void RegisterParser(IFormatParser parser)
    {
        Guard.NotNull(parser);
        _instance.RegisteredParsers.Add(parser);
    }

    public void RegisterExporter(IFormatExporter exporter)
    {
        Guard.NotNull(exporter);
        _instance.RegisteredExporters.Add(exporter);
    }

    public void RegisterDetector(IFormatDetector detector)
    {
        Guard.NotNull(detector);
        _instance.RegisteredDetectors.Add(detector);
    }

    public void RegisterValidator(IFormatValidator validator)
    {
        Guard.NotNull(validator);
        _instance.RegisteredValidators.Add(validator);
    }
}

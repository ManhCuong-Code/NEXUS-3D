using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Plugin.SDK.Contracts;

/// <summary>
/// Registry contract exposed to plugins during initialization for contributing 3D format capabilities.
/// </summary>
public interface IPluginRegistrationContext
{
    /// <summary>
    /// Registers a custom format descriptor describing file extensions, native geometry, and metadata.
    /// </summary>
    void RegisterDescriptor(IFormatDescriptor descriptor);

    /// <summary>
    /// Registers a format parser for decoding streams into the Universal Scene Model (<see cref="NEXUS3D.Domain.Scene.Scene3D"/>).
    /// </summary>
    void RegisterParser(IFormatParser parser);

    /// <summary>
    /// Registers a format exporter for serializing the Universal Scene Model to target files or streams.
    /// </summary>
    void RegisterExporter(IFormatExporter exporter);

    /// <summary>
    /// Registers a format detector for identifying formats and versions from stream headers.
    /// </summary>
    void RegisterDetector(IFormatDetector detector);

    /// <summary>
    /// Registers a format validator for verifying stream structural integrity.
    /// </summary>
    void RegisterValidator(IFormatValidator validator);
}


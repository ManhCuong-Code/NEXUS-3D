using NEXUS3D.Plugin.SDK.Attributes;
using NEXUS3D.Plugin.SDK.Base;
using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Sample.Plugin;

/// <summary>
/// Entrypoint implementation for the RawVox 3D plugin extension.
/// </summary>
[NexusPlugin("com.nexus3d.formats.rawvox", "RawVox 3D Format Extension", "1.0.0")]
public sealed class RawVoxPlugin : NexusPluginBase
{
    public RawVoxPlugin() : base(new PluginManifest
    {
        Id = "com.nexus3d.formats.rawvox",
        Name = "RawVox 3D Format Extension",
        Version = "1.0.0",
        Author = "NEXUS-3D Core Extension Team",
        Description = "Adds full import, export, and conversion capabilities for the RawVox 3D text voxel model format.",
        Assembly = "NEXUS3D.Sample.Plugin.dll",
        EntryPoint = "NEXUS3D.Sample.Plugin.RawVoxPlugin",
        PluginApiVersion = "1.0.0",
        MinimumHostVersion = "1.0.0",
        SupportedFormats = ["rawvox", "rvox"],
        Capabilities = ["FormatParser", "FormatExporter", "FormatDetector", "FormatValidator"],
        License = "MIT"
    })
    {
    }

    protected override Task<OperationResult> OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Registering RawVox format components into the NEXUS-3D ecosystem...");

        Registration?.RegisterDescriptor(new RawVoxFormatDescriptor());
        Registration?.RegisterParser(new RawVoxParser());
        Registration?.RegisterExporter(new RawVoxExporter());
        Registration?.RegisterDetector(new RawVoxDetector());
        Registration?.RegisterValidator(new RawVoxValidator());

        Logger?.LogInformation("RawVox 3D format successfully registered and ready for conversion!");
        return Task.FromResult(OperationResult.Success());
    }

    protected override Task<OperationResult> OnShutdownAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("RawVox 3D format extension unloaded.");
        return Task.FromResult(OperationResult.Success());
    }
}

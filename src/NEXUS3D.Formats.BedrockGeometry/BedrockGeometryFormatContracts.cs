using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry;

/// <summary>
/// Format descriptor for Minecraft Bedrock Geometry models (.geo.json).
/// </summary>
public sealed class BedrockGeometryFormatDescriptor : IFormatDescriptor
{
    public ModelFormatType FormatType => ModelFormatType.BedrockGeometry;
    public string DisplayName => "Bedrock Geometry JSON";
    public string ShortCode => "GEO";
    public string PrimaryExtension => ".geo.json";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".geo.json"];
    public GeometryKind NativeGeometryKind => GeometryKind.CuboidHierarchy;
    public bool SupportsImport => true;
    public bool SupportsExport => true;
    public string Description => "Hierarchical bone, pivot, and cube geometry specification for Bedrock Engine.";

    public bool CanHandleExtension(string filePathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExtension))
        {
            return false;
        }

        string normalized = filePathOrExtension.Trim().ToLowerInvariant();
        return SupportedExtensions.Any(ext => normalized.EndsWith(ext, StringComparison.Ordinal));
    }
}

/// <summary>
/// Specialized contract for Bedrock Geometry parsers (Stage 02+).
/// </summary>
public interface IBedrockGeometryParser : IModelFormatParser
{
    string DefaultFormatVersion { get; }
}

/// <summary>
/// Specialized contract for Bedrock Geometry exporters (Stage 02+).
/// </summary>
public interface IBedrockGeometryExporter : IModelFormatExporter
{
    string TargetFormatVersion { get; }
}


using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Formats.VoxelJson;

/// <summary>
/// Format descriptor for Java Block/Item Voxel JSON models (.voxel.json, .json).
/// </summary>
public sealed class VoxelJsonFormatDescriptor : IFormatDescriptor
{
    public ModelFormatType FormatType => ModelFormatType.VoxelJson;
    public string DisplayName => "Voxel Cuboid JSON";
    public string ShortCode => "JSON";
    public string PrimaryExtension => ".json";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".voxel.json", ".json"];
    public GeometryKind NativeGeometryKind => GeometryKind.CuboidHierarchy;
    public bool SupportsImport => true;
    public bool SupportsExport => true;
    public string Description => "Cuboid element and UV texture definition format used by block/item models.";

    public bool CanHandleExtension(string filePathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExtension))
        {
            return false;
        }

        string normalized = filePathOrExtension.Trim().ToLowerInvariant();
        if (normalized.EndsWith(".geo.json", StringComparison.Ordinal))
        {
            return false;
        }

        return SupportedExtensions.Any(ext => normalized.EndsWith(ext, StringComparison.Ordinal));
    }
}

/// <summary>
/// Specialized contract for Voxel JSON parsers (Stage 02+).
/// </summary>
public interface IVoxelJsonParser : IModelFormatParser
{
    int MaxAllowedElements { get; }
}

/// <summary>
/// Specialized contract for Voxel JSON exporters (Stage 02+).
/// </summary>
public interface IVoxelJsonExporter : IModelFormatExporter
{
    bool PrettyPrintOutput { get; }
}


using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Formats.Schematic;

/// <summary>
/// Format descriptor for Minecraft Schematic formats (.schem, .schematic, .litematic).
/// </summary>
public sealed class SchematicFormatDescriptor : IFormatDescriptor
{
    public ModelFormatType FormatType => ModelFormatType.Schematic;
    public string DisplayName => "Minecraft Schematic";
    public string ShortCode => "SCHEM";
    public string PrimaryExtension => ".schem";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".schem", ".schematic", ".litematic"];
    public GeometryKind NativeGeometryKind => GeometryKind.VoxelGrid;
    public bool SupportsImport => true;
    public bool SupportsExport => true;
    public string Description => "Voxel block grid structure used by WorldEdit, Sponge, and Litematica.";

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
/// Specialized contract for Sponge/MCEdit schematic parsers (Stage 02+).
/// </summary>
public interface ISchematicParser : IModelFormatParser
{
    IReadOnlyList<int> SupportedDataVersions { get; }
}

/// <summary>
/// Specialized contract for Sponge/MCEdit schematic exporters (Stage 02+).
/// </summary>
public interface ISchematicExporter : IModelFormatExporter
{
    bool CompressWithGZip { get; }
}


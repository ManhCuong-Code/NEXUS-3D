using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Sample.Plugin;

/// <summary>
/// Format descriptor for the RawVox 3D voxel format contributed by this plugin.
/// </summary>
public sealed class RawVoxFormatDescriptor : IFormatDescriptor
{
    public ModelFormatType FormatType => ModelFormatType.Custom;
    public string DisplayName => "RawVox 3D (*.rawvox, *.rvox)";
    public string ShortCode => "RAWVOX";
    public string PrimaryExtension => ".rawvox";
    public IReadOnlyList<string> SupportedExtensions => [".rawvox", ".rvox"];
    public GeometryKind NativeGeometryKind => GeometryKind.VoxelGrid;
    public bool SupportsImport => true;
    public bool SupportsExport => true;
    public string Description => "Lightweight text-based 3D voxel and polygon format for NEXUS-3D plugin extension demonstration.";

    public bool CanHandleExtension(string filePathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExtension)) return false;
        string ext = Path.GetExtension(filePathOrExtension);
        if (string.IsNullOrEmpty(ext)) ext = filePathOrExtension;
        return SupportedExtensions.Any(s => string.Equals(s, ext, StringComparison.OrdinalIgnoreCase));
    }
}


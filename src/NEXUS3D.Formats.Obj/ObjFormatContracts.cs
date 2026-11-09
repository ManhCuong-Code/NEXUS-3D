using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Formats.Obj;

/// <summary>
/// Format descriptor for Wavefront OBJ polygon meshes (.obj).
/// </summary>
public sealed class ObjFormatDescriptor : IFormatDescriptor
{
    public ModelFormatType FormatType => ModelFormatType.Obj;
    public string DisplayName => "Wavefront OBJ Mesh";
    public string ShortCode => "OBJ";
    public string PrimaryExtension => ".obj";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".obj"];
    public GeometryKind NativeGeometryKind => GeometryKind.PolygonMesh;
    public bool SupportsImport => true;
    public bool SupportsExport => true;
    public string Description => "Standard 3D polygon mesh format with companion MTL material definitions.";

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
/// Specialized contract for Wavefront OBJ parsers (Stage 02+).
/// </summary>
public interface IObjParser : IModelFormatParser
{
    bool ResolveCompanionMtl { get; }
}

/// <summary>
/// Specialized contract for Wavefront OBJ exporters (Stage 02+).
/// </summary>
public interface IObjExporter : IModelFormatExporter
{
    bool EmitNormals { get; }
    bool EmitTexCoords { get; }
}


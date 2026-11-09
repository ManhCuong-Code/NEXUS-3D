using NEXUS3D.Formats.Obj.Exporting;

namespace NEXUS3D.Formats.Obj.Conversion;

/// <summary>
/// Options controlling bidirectional mapping between Wavefront OBJ/MTL documents and the Universal Scene Model (<see cref="NEXUS3D.Domain.Scene.Scene3D"/>).
/// </summary>
public sealed record ObjMappingOptions
{
    public bool TriangulatePolygons { get; init; }
    public bool LoadCompanionMtl { get; init; } = true;
    public bool ValidateTextures { get; init; } = true;
    public bool PreserveRawData { get; init; } = true;
    public bool StrictMode { get; init; }
    public string? SourceFileName { get; init; }
    public long SourceFileSizeBytes { get; init; }
    public string? BaseDirectory { get; init; }
    public ObjExportOptions ExportOptions { get; init; } = ObjExportOptions.Default;

    public static ObjMappingOptions Default { get; } = new();
}

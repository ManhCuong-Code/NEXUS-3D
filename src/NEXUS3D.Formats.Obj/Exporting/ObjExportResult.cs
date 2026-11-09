using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Formats.Obj.Exporting;

/// <summary>
/// Result of exporting a Wavefront OBJ document and optional companion MTL / texture files.
/// </summary>
public sealed class ObjExportResult
{
    public bool Succeeded { get; init; }
    public string? ObjFilePath { get; init; }
    public string? MtlFilePath { get; init; }
    public IReadOnlyList<string> CopiedTexturePaths { get; init; } = [];
    public int VertexCount { get; init; }
    public int TextureCoordCount { get; init; }
    public int NormalCount { get; init; }
    public int FaceCount { get; init; }
    public int MaterialCount { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    public TimeSpan Elapsed { get; init; }
}


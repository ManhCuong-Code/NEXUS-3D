namespace NEXUS3D.Formats.Obj.Parsing;

/// <summary>
/// Configuration and safety limits for parsing Wavefront OBJ streams and files.
/// </summary>
public sealed record ObjParseOptions
{
    public static ObjParseOptions Default { get; } = new();

    public long MaxFileSizeBytes { get; init; } = 256L * 1024L * 1024L; // 256 MB
    public int MaxLineLength { get; init; } = 65_536;                   // 64 KB per logical line
    public int MaxVertexCount { get; init; } = 16_777_216;              // 16M vertices
    public int MaxFaceCount { get; init; } = 16_777_216;                // 16M faces

    public bool StrictValidation { get; init; } = true;
    public bool LoadCompanionMtl { get; init; } = true;
    public bool ValidateTextureFilesExist { get; init; } = true;
    public bool RecalculateMissingNormals { get; init; } = false;
    public bool ForceRecalculateNormals { get; init; } = false;
    public bool TriangulatePolygons { get; init; } = false;
    public bool PreserveUnrecognizedStatements { get; init; } = true;

    public string? SourceFilePath { get; init; }
    public string? BaseDirectory { get; init; }
}

namespace NEXUS3D.Formats.Abstractions;

/// <summary>
/// Common options for parsing 3D/schematic files into the Universal Scene Model (USM).
/// </summary>
public record ParseOptions
{
    public static ParseOptions Default { get; } = new();

    public bool StrictValidation { get; init; } = true;
    public bool PreserveUnknownData { get; init; } = true;
    public long MaxInputBytes { get; init; } = 256L * 1024L * 1024L;
    public int MaxNestingDepth { get; init; } = 128;
    public string? SourceFileName { get; init; }
}

/// <summary>
/// Common options for exporting a <see cref="NEXUS3D.Domain.Scene.Scene3D"/> to an external file format.
/// </summary>
public record ExportOptions
{
    public static ExportOptions Default { get; } = new();

    public bool StrictMapping { get; init; }
    public bool PreserveUnknownData { get; init; } = true;
    public bool VerifyAfterWrite { get; init; } = true;
    public bool AllowOverwrite { get; init; } = true;
}


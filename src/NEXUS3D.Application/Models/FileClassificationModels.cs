using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Application.Models;

/// <summary>
/// Broad categorization of 3D data formats to direct models to their specialized workspace.
/// </summary>
public enum FormatCategory
{
    Unknown = 0,
    PolygonMesh = 1,
    MinecraftSchematic = 2,
    VoxelModel = 3,
    BedrockGeometry = 4,
    PluginExtension = 5
}

/// <summary>
/// Confidence score for format detection.
/// </summary>
public enum ConfidenceLevel
{
    Unsupported = 0,
    Ambiguous = 1,
    Probable = 2,
    Definite = 3
}

/// <summary>
/// Detailed capabilities supported by a classified file format.
/// </summary>
public sealed record FormatCapabilities(
    bool SupportsPolygons,
    bool SupportsVoxels,
    bool SupportsBones,
    bool SupportsMaterials,
    bool SupportsTextures,
    bool SupportsPalette,
    bool SupportsAnimations)
{
    public static FormatCapabilities MeshDefaults => new(
        SupportsPolygons: true,
        SupportsVoxels: false,
        SupportsBones: false,
        SupportsMaterials: true,
        SupportsTextures: true,
        SupportsPalette: false,
        SupportsAnimations: false);

    public static FormatCapabilities SchematicDefaults => new(
        SupportsPolygons: false,
        SupportsVoxels: true,
        SupportsBones: false,
        SupportsMaterials: true,
        SupportsTextures: false,
        SupportsPalette: true,
        SupportsAnimations: false);

    public static FormatCapabilities VoxelDefaults => new(
        SupportsPolygons: false,
        SupportsVoxels: true,
        SupportsBones: false,
        SupportsMaterials: true,
        SupportsTextures: false,
        SupportsPalette: true,
        SupportsAnimations: false);

    public static FormatCapabilities BedrockDefaults => new(
        SupportsPolygons: true,
        SupportsVoxels: false,
        SupportsBones: true,
        SupportsMaterials: true,
        SupportsTextures: true,
        SupportsPalette: false,
        SupportsAnimations: true);

    public static FormatCapabilities GenericDefaults => new(
        SupportsPolygons: true,
        SupportsVoxels: true,
        SupportsBones: true,
        SupportsMaterials: true,
        SupportsTextures: true,
        SupportsPalette: true,
        SupportsAnimations: false);
}

/// <summary>
/// Comprehensive pre-flight inspection report generated prior to opening a model file.
/// </summary>
public sealed class FileClassificationReport
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string FormattedFileSize { get; init; }

    public ModelFormatType FormatType { get; set; }
    public FormatCategory Category { get; set; }
    public required string FormatDisplayName { get; set; }
    public required string ShortCode { get; set; }
    public string VersionString { get; set; } = "1.0";
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Probable;
    public string Details { get; set; } = string.Empty;

    public string ParserName { get; init; } = "Universal Scene Parser";
    public FormatCapabilities Capabilities { get; init; } = FormatCapabilities.GenericDefaults;

    public List<string> ReadableComponents { get; init; } = [];
    public List<string> PotentialWarnings { get; init; } = [];

    public bool CanOpen => FormatType != ModelFormatType.Unknown && Confidence != ConfidenceLevel.Unsupported;
    public IFormatDescriptor? Descriptor { get; init; }
}

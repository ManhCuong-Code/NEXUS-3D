using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Conversion;

/// <summary>
/// Options controlling conversion between <see cref="SchematicDocument"/> and USM <see cref="NEXUS3D.Domain.Scene.Scene3D"/>.
/// </summary>
public sealed record SchematicMappingOptions
{
    public static SchematicMappingOptions Default { get; } = new();

    public bool IncludeAirVoxels { get; init; } = false;
    public bool PreserveRawNbtData { get; init; } = true;
    public SchematicFormatId TargetFormat { get; init; } = SchematicFormatId.SpongeV2;
    public int DefaultDataVersion { get; init; } = 3465;
    public bool StrictLegacyMapping { get; init; } = false;
    public string? SourceFileName { get; init; }
    public long SourceFileSizeBytes { get; init; }
}


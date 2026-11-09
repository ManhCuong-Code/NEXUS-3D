using NEXUS3D.Formats.Schematic.Nbt.IO;

namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Options controlling schematic parsing, memory limits, and unknown data preservation.
/// </summary>
public sealed record SchematicReadOptions
{
    public static SchematicReadOptions Default { get; } = new();

    public NbtReadOptions NbtOptions { get; init; } = NbtReadOptions.Default;
    public int MaxDimensionSize { get; init; } = 4096;
    public int MaxTotalBlocks { get; init; } = 67_108_864; // 64M blocks (e.g. 512x256x512)
    public bool StrictValidation { get; init; } = true;
    public bool StrictLegacyMapping { get; init; } = false;
    public bool PreserveUnknownData { get; init; } = true;
    public bool IncludeAirVoxelsInUsm { get; init; } = false;
    public string? SourceFileName { get; init; }
}

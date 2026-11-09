using NEXUS3D.Domain.Metadata;
using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Conversion;

/// <summary>
/// Detailed report produced during <see cref="SchematicDocument"/> &lt;-&gt; USM <see cref="NEXUS3D.Domain.Scene.Scene3D"/> conversion.
/// </summary>
public sealed class SchematicConversionReport
{
    public SchematicFormatId Format { get; init; }
    public SchematicDimensions Dimensions { get; init; }
    public int TotalVolumeBlocks => Dimensions.Width * Dimensions.Height * Dimensions.Length;
    public int PopulatedVoxelCount { get; init; }
    public int PaletteEntryCount { get; init; }
    public int BlockEntityCount { get; init; }
    public int PreservedRawContainerCount { get; init; }
    public IReadOnlyList<WarningRecord> Warnings { get; init; } = [];
    public LossReport LossReport { get; init; } = new();
}

